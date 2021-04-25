using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PMCommonApiModels.RequestModels;
using PMCommonEntities.Models;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.TradingExt;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Implementation
{
    public class RelationalDataStoreRepository : IRelationalDataStoreRepository
    {
        private readonly PseudoMarketsDbContext _dbContext;
        public RelationalDataStoreRepository(PseudoMarketsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Orders> CreateOrder(string symbol, string type, double price, int quantity, DateTime date, string transactionId, RDSEnums.EnvironmentId environmentId, RDSEnums.OriginId originId, RDSEnums.SecurityType securityType)
        {
            Orders order = new Orders
            {
                Symbol = symbol,
                Type = type,
                Price = price,
                Quantity = quantity,
                Date = date,
                TransactionID = transactionId,
                EnvironmentId = environmentId,
                OriginId = originId,
                SecurityTypeId = securityType
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            return order;
        }

        public async Task<QueuedOrders> CreateQueuedOrder(string symbol, string type, int quantity, RDSEnums.EnvironmentId environmentId, bool isOpenOrder, DateTime orderDate, int accountId)
        {
            QueuedOrders queuedOrder = new QueuedOrders()
            {
                Symbol = symbol,
                OrderType = type,
                EnvironmentId = environmentId,
                IsOpenOrder = isOpenOrder,
                Quantity = quantity,
                OrderDate = orderDate,
                UserId = accountId
            };

            _dbContext.QueuedOrders.Add(queuedOrder);
            await _dbContext.SaveChangesAsync();

            return queuedOrder;
        }

        public async Task<IEnumerable<QueuedOrders>> GetQueuedOrders(DateTime orderDate)
        {
            return _dbContext.QueuedOrders.Where(x => x.IsOpenOrder && x.OrderDate == orderDate).ToList();
        }

        public async Task<bool> MarketHolidayCheck()
        {
            var isHoliday = _dbContext.MarketHolidays.Any(x => x.HolidayDate == DateTime.Today);
            return isHoliday;
        }

        public async Task<Transactions> CreateTransaction(int accountId, RDSEnums.EnvironmentId environmentId,
            RDSEnums.OriginId originId)
        {
            var transaction = new Transactions()
            {
                AccountId = accountId,
                EnvironmentId = environmentId,
                TransactionId = Guid.NewGuid().ToString(),
                OriginId = originId
            };

            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            return transaction;
        }
        
        public async Task<Positions> CheckAndGetExistingPosition(Accounts account, string symbol)
        {
            return _dbContext.Positions.FirstOrDefault(x => x.AccountId == account.Id && x.Symbol == symbol);
        }
        
        public async Task UpdatePosition(Positions existingPosition, double newValue, int newQuantity, Accounts account, double newAccountBalance)
        {
            existingPosition.Value = newValue;
            existingPosition.Quantity = newQuantity;

            account.Balance = newAccountBalance;

            _dbContext.Entry(existingPosition).State = EntityState.Modified;
            _dbContext.Entry(account).State = EntityState.Modified;

            await _dbContext.SaveChangesAsync();
        }

        public async Task LiquidatePosition(Positions existingPosition, Accounts account, double newBalance)
        {
            _dbContext.Entry(existingPosition).State = EntityState.Deleted;
            account.Balance = newBalance;

            await _dbContext.SaveChangesAsync();
        }

        public async Task CreatePosition(Positions newPosition, Accounts account, double newBalance)
        {
            _dbContext.Positions.Add(newPosition);
            account.Balance = newBalance;
            _dbContext.Entry(account).State = EntityState.Modified;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteInvalidOrder(Orders invalidOrder)
        {
            _dbContext.Entry(invalidOrder).State = EntityState.Deleted;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<Orders> GetOrderUsingTransactionId(string transactionId)
        {
            return _dbContext.Orders.FirstOrDefault(x => x.TransactionID == transactionId);
        }

        public async Task CreateQueuedOrder(TradeExecInput tradeInput, Accounts account)
        {
            throw new NotImplementedException();
        }

        public async Task<Accounts> GetAccountUsingId(int accountId)
        {
            return _dbContext.Accounts.FirstOrDefault(x => x.Id == accountId);
        }
    }
}
