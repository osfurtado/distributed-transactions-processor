namespace AccountTransactionsProcessor.Dtos
{
    public record struct TransactionRequest(decimal Amount, char Type, string Description);
}
