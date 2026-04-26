namespace AccountTransactionsProcessor.Dtos
{
    public record struct AccountStatementResult(int Limit, int Total, DateTime Date_Statement);
}
