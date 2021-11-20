using Aerospike.Client;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Interfaces;

namespace PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Implementations
{
    public class RealTimeDbContext : IRealTimeDbContext
    {
        private readonly IAerospikeClient _client;
        private readonly Policy _defaultReadPolicy = new Policy() { sendKey = true };
        private readonly WritePolicy _defaultWritePolicy = new WritePolicy() { sendKey = true, expiration = -1 };

        public RealTimeDbContext(IAerospikeClient client)
        {
            _client = client;
        }

        public RealTimeDbContext(string host, int port)
        {
            _client = new AerospikeClient(host, port);
        }

        public Record Get(string nsName, string setName, string primaryKey)
        {
            return _client.Get(_defaultReadPolicy, new Key(nsName, setName, primaryKey));
        }

        public void Insert(string nsName, string setName, string primaryKey, Bin[] bins)
        {
            _client.Put(_defaultWritePolicy, new Key(nsName, setName, primaryKey), bins);
        }

        public void Delete(string nsName, string setName, string primaryKey)
        {
            _client.Delete(_defaultWritePolicy, new Key(nsName, setName, primaryKey));
        }

        public bool IsConnected()
        {
            return _client.Connected;
        }
    }
}
