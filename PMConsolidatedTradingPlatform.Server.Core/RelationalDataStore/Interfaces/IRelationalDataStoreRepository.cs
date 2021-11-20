using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PMCommonApiModels.RequestModels;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces
{
    public interface IRelationalDataStoreRepository
    {
        Task<Orders> CreateOrder(string symbol, string type, double price, int quantity, DateTime date,
            string transactionId, RDSEnums.EnvironmentId environmentId, RDSEnums.OriginId originId,
            RDSEnums.SecurityType securityType);
        
        Task UpdatePosition(Positions existingPosition, double newValue, int newQuantity, Accounts account,
            double newAccountBalance);

        Task<Accounts> GetAccountUsingId(int accountId);

        Task<Transactions> CreateTransaction(int accountId, RDSEnums.EnvironmentId environmentId,
            RDSEnums.OriginId originId);

        Task<Positions> CheckAndGetExistingPosition(Accounts account, string symbol);

        Task LiquidatePosition(Positions existingPosition, Accounts account, double newBalance);

        Task<Orders> GetOrderUsingTransactionId(string transactionId);

        Task CreatePosition(Positions newPosition, Accounts account, double newBalance);

        Task DeleteInvalidOrder(Orders invalidOrder);

        Task<QueuedOrders> CreateQueuedOrder(string symbol, string type, int quantity,
            RDSEnums.EnvironmentId environmentId, bool isOpenOrder, DateTime orderDate, int accountId);

        Task<IEnumerable<QueuedOrders>> GetAllQueuedOrders(DateTime orderDate);

        Task CancelAllQueuedOrders(DateTime orderDate);

        Task<IEnumerable<QueuedOrders>> GetQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate);

        Task CancelQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate);

        Task<bool> MarketHolidayCheck();

        Task<IEnumerable<QueuedOrders>> GetAndDrainAllQueuedOrders(DateTime orderDate);

        Task<IEnumerable<QueuedOrders>> GetAndDrainQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate);

        Task<IEnumerable<TradeLots>> GetTradeLots(Accounts account, string symbol);

        Task CreateTradeLot(Positions position, RDSEnums.TradeSide tradeSide, bool isLiquidatingPosition, double tradePrice, double tradeQuantity);
    }
}
