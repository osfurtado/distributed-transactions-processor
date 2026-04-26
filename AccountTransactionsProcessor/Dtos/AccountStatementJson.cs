namespace AccountTransactionsProcessor.Dtos
{
    public record struct AccountStatementJson(AccountStatementResult? balance, List<TransactionStatementResult> last_transactions);
}
