using AccountTransactionsProcessor.Dtos;
using AccountTransactionsProcessor.Pools;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var connectionString = builder.Configuration.GetConnectionString("DbProd");

var dataSource = new NpgsqlDataSourceBuilder(connectionString)
    .Build();

builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<AccountCreditPool>();
builder.Services.AddSingleton<AccountDebitPool>();
builder.Services.AddSingleton<AccountStatementPool>();

builder.Services.AddSingleton<IHostedService, PoolStartup>();

builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(60) });

var app = builder.Build();


app.MapPost("/customers/{id}/transactions", async Task<Results<Ok<PostTransactionResult>, NotFound, UnprocessableEntity>> (
    [FromRoute] int id,
    [FromBody] TransactionRequest request,
    AccountDebitPool debitTransactionPool,
    AccountCreditPool creditTransactionPool) =>
{
    if (string.IsNullOrEmpty(request.Description) || request.Description.Length > 10)
    {
        return TypedResults.UnprocessableEntity();
    }

    if (request.Type != 'c' && request.Type != 'd')
    {
        return TypedResults.UnprocessableEntity();
    }

    if (request.Amount <= 0 || request.Amount % 1 != 0)
    {
        return TypedResults.UnprocessableEntity();
    }

    var result = new PostTransactionResult(0, 0);

    NpgsqlCommand command = null;

    try
    {

        if (request.Type == 'd')
            command = debitTransactionPool.GetCommand();
        else
            command = creditTransactionPool.GetCommand();

        command.Parameters[0].Value = (int)request.Amount;
        command.Parameters[1].Value = id;
        command.Parameters[2].Value = request.Type;
        command.Parameters[3].Value = request.Description;

        await using var connection = await dataSource.OpenConnectionAsync();
        command.Connection = connection;

        using var reader = await command.ExecuteReaderAsync();

        bool hasRows = false;

        while (await reader.ReadAsync())
        {
            hasRows = true;
            result.Balance = reader.GetInt32(0);
            result.Limit = reader.GetInt32(1);
        }
        if (!hasRows)
            return TypedResults.UnprocessableEntity();
    }
    finally
    {
        command.Connection = null;

        if (request.Type == 'd')
            debitTransactionPool.ReturnCommand(command);
        else
            creditTransactionPool.ReturnCommand(command);
    }
    return TypedResults.Ok(result);
}).AddEndpointFilter(async (ctx, next) =>
{
    var path = ctx.HttpContext.Request.Path;
    var segments = path.Value?.Split('/');

    if (segments == null || segments.Length < 3
        || !int.TryParse(segments[2], out int digit)
        || digit < 1
        || digit > 5)
    {
        return TypedResults.NotFound();
    }

    return await next(ctx);
});

app.MapGet("/customers/{id}/statement", async Task<Results<Ok<AccountStatementJson>, NotFound, UnprocessableEntity>> ([FromRoute] int id, AccountStatementPool statementPool) =>
{
    var commands = statementPool.GetCommand();

    commands.TryGetValue("selectCustomer", out NpgsqlCommand? selectCustomerCommand);

    selectCustomerCommand.Parameters[0].Value = id;

    await using var connection = await dataSource.OpenConnectionAsync();
    selectCustomerCommand.Connection = connection;

    var balanceStatementResult = new AccountStatementResult();
    using (var readerCustomer = await selectCustomerCommand.ExecuteReaderAsync())
    {
        if (!readerCustomer.HasRows)
        {
            await readerCustomer.CloseAsync();
            await readerCustomer.DisposeAsync();
            selectCustomerCommand.Connection = null;
            statementPool.ReturnCommand(commands);

            return TypedResults.NotFound();
        }

        while (await readerCustomer.ReadAsync())
        {
            balanceStatementResult.Limit = readerCustomer.GetInt32(0);
            balanceStatementResult.Total = readerCustomer.GetInt32(1);
            balanceStatementResult.Date_Statement = DateTime.Now;
        }
    }

    commands.TryGetValue("selectTransaction", out NpgsqlCommand? selectTransactionsCommand);

    var transactions = new List<TransactionStatementResult>();

    selectTransactionsCommand.Parameters[0].Value = id;

    selectTransactionsCommand.Connection = connection;

    using (var readerTransactions = await selectTransactionsCommand.ExecuteReaderAsync())
    {
        if (readerTransactions.HasRows)
        {
            while (await readerTransactions.ReadAsync())
            {
                var t = new TransactionStatementResult(
                    readerTransactions.GetInt32(0),
                    readerTransactions.GetChar(1),
                    readerTransactions.GetString(2),
                    readerTransactions.GetDateTime(3));

                transactions.Add(t);
            }
        }
    }

    selectCustomerCommand.Connection = null;
    selectTransactionsCommand.Connection = null;
    statementPool.ReturnCommand(commands);

    return TypedResults.Ok(new AccountStatementJson(balanceStatementResult, transactions));
}).AddEndpointFilter(async (ctx, next) =>
{
    var path = ctx.HttpContext.Request.Path;
    var segments = path.Value?.Split('/');

    if (segments?.Length == 0 || !(int.TryParse(segments?[1], out int digit) || digit < 1 || digit > 5))
    {
        return TypedResults.NotFound();
    }

    return await next(ctx);
});



app.Run();


[JsonSerializable(typeof(TransactionRequest))]
[JsonSerializable(typeof(TransactionResult))]
[JsonSerializable(typeof(AccountStatementResult))]
[JsonSerializable(typeof(TransactionStatementResult))]
[JsonSerializable(typeof(AccountStatementJson))]
[JsonSerializable(typeof(PostTransactionResult))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
