using Npgsql;
using NpgsqlTypes;
using System.Collections.Concurrent;

namespace AccountTransactionsProcessor.Pools
{
    public class AccountStatementPool
    {
        private readonly ConcurrentQueue<Dictionary<string, NpgsqlCommand>> _pool;

        private int POOL_SIZE = int.Parse(Environment.GetEnvironmentVariable("POOL_SIZE") ?? "1");

        public int Count => _pool.Count;

        public AccountStatementPool()
        {
            _pool = Fill();
        }

        public ConcurrentQueue<Dictionary<string, NpgsqlCommand>> Fill()
        {
            var pool = new ConcurrentQueue<Dictionary<string, NpgsqlCommand>>();
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var cmd = Create();
                pool.Enqueue(cmd);
            }
            return pool;
        }


        public Dictionary<string, NpgsqlCommand> Create()
        {
            var selectCustomer = new NpgsqlCommand(@"SELECT ""limit"", balance FROM customer WHERE id = $1;");
            selectCustomer.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

            var selectTransaction = new NpgsqlCommand(@"SELECT amount, type as type, description, created_at FROM transactions WHERE customer_id = $1 ORDER BY id DESC LIMIT 10;");
            selectTransaction.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

            return new Dictionary<string, NpgsqlCommand>
        {
            {"selectCustomer", selectCustomer },
            {"selectTransaction", selectTransaction }
        };
        }


        public Dictionary<string, NpgsqlCommand> GetCommand()
        {
            Console.WriteLine($"The AccountStatementPool queue has: {_pool.Count}");

            if (!_pool.IsEmpty)
            {

                if (_pool.TryDequeue(out var result))
                    return result;
                return Create();
            }

            return Create();
        }


        public void ReturnCommand(Dictionary<string, NpgsqlCommand> dict)
        {
            dict["selectCustomer"].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
            dict["selectTransaction"].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };

            _pool.Enqueue(dict);
        }

    }
}
