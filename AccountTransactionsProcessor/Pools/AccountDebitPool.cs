using Npgsql;
using NpgsqlTypes;
using System.Collections.Concurrent;

namespace AccountTransactionsProcessor.Pools
{
    public class AccountDebitPool
    {
        private readonly ConcurrentQueue<NpgsqlCommand> _pool;
        private int POOL_SIZE = int.Parse(Environment.GetEnvironmentVariable("POOL_SIZE") ?? "1");

        public AccountDebitPool()
        {
            _pool = Fill();
        }


        public ConcurrentQueue<NpgsqlCommand> Fill()
        {
            Console.WriteLine("Starting to fill AccountDebitPool");

            var pool = new ConcurrentQueue<NpgsqlCommand>();
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var cmd = Create();
                pool.Enqueue(cmd);
            }

            Console.WriteLine("Pool filled");
            return pool;
        }


        public NpgsqlCommand Create()
        {
            var command = new NpgsqlCommand(@"
                            WITH updated AS (
                                UPDATE customer SET balance = balance - @amount
                                WHERE id = @id AND balance - @amount >= -limit
                                RETURNING balance, "limit"
                            ),
                            ins AS (INSERT INTO transactions (amount, description, type, customer_id)
                                SELECT @amount, @description, @type, @id
                                FROM updated)
                            SELECT balance, "limit" FROM updated;");

            command.Parameters.Add(new NpgsqlParameter("amount", NpgsqlDbType.Integer));
            command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer));
            command.Parameters.Add(new NpgsqlParameter("type", NpgsqlDbType.Char));
            command.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Varchar));

            return command;
        }

        public NpgsqlCommand GetCommand()
        {
            Console.WriteLine($"The Pool queue has: {_pool.Count}");

            if (!_pool.IsEmpty)
            {

                if (_pool.TryDequeue(out var result))
                    return result;
                return Create();
            }

            return Create();
        }


        public void ReturnCommand(NpgsqlCommand command)
        {
            // Reset before returning it to the pool
            command.Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
            command.Parameters[1] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
            command.Parameters[2] = new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Char };
            command.Parameters[3] = new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar };

            _pool.Enqueue(command);
        }


    }
}
