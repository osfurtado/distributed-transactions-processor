namespace AccountTransactionsProcessor.Dtos
{
    public record struct TransactionStatementResult(int Amount, char Type, string Description, DateTime Executed_At);
}
