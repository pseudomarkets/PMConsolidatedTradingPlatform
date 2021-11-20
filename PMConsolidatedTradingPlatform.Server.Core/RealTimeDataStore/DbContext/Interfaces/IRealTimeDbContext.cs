using Aerospike.Client;

namespace PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Interfaces
{
    public interface IRealTimeDbContext
    {
        Record Get(string nsName, string setName, string primaryKey);

        void Insert(string nsName, string setName, string primaryKey, params Bin[] bins);

        void Delete(string nsName, string setName, string primaryKey);

        bool IsConnected();
    }
}
