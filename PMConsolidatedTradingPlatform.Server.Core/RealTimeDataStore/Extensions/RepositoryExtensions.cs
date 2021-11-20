using Microsoft.Extensions.DependencyInjection;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Interfaces;

namespace PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Extensions
{
    public static class RepositoryExtensions
    {
        public static void AddRealTimeDataStoreRepositories(this IServiceCollection collection, IRealTimeDbContext dbContext)
        {
            collection.AddSingleton(dbContext);
            collection.AddSingleton<IExtendedTransactionsRepository>();
        }
    }
}
