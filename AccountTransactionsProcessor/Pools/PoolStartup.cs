namespace AccountTransactionsProcessor.Pools
{
    public class PoolStartup (
        AccountCreditPool accountCreditPool,
        AccountDebitPool accountDebitPool,
        AccountStatementPool accountStatementPool
        ): IHostedService
    {
        private readonly AccountCreditPool _accountCreditPool = accountCreditPool;
        private readonly AccountDebitPool _accountDebitPool = accountDebitPool;
        private readonly AccountStatementPool _accountStatementPool = accountStatementPool;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"CreditPool ready: {_accountCreditPool.Count} commands");
            Console.WriteLine($"DebitPool ready: {_accountDebitPool.Count} commands");
            Console.WriteLine($"StatementPool ready: {_accountStatementPool.Count} commands");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }


    }
}
