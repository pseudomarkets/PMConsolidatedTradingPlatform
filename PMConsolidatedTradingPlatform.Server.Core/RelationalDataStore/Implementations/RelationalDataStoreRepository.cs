using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PMCommonEntities.Models;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Implementations
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

        public async Task<IEnumerable<QueuedOrders>> GetAllQueuedOrders(DateTime orderDate)
        {
            return _dbContext.QueuedOrders.Where(x => x.IsOpenOrder && x.OrderDate == orderDate).ToList();
        }

        public async Task CancelAllQueuedOrders(DateTime orderDate)
        {
            var orders = await GetAllQueuedOrders(orderDate);

            foreach (var order in orders)
            {
                order.IsOpenOrder = false;

                _dbContext.Entry(order).State = EntityState.Modified;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<IEnumerable<QueuedOrders>> GetQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate)
        {
            var orders = _dbContext.QueuedOrders.Where(x => orderIds.Contains(x.Id) && x.OrderDate == orderDate).ToList();

            return orders;
        }

        public async Task CancelQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate)
        {
            var orders = await GetQueuedOrders(orderIds, orderDate);

            foreach (var order in orders)
            {
                order.IsOpenOrder = false;

                _dbContext.Entry(order).State = EntityState.Modified;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> MarketHolidayCheck()
        {
            var isHoliday = _dbContext.MarketHolidays.Any(x => x.HolidayDate == DateTime.Today);
            return isHoliday;
        }

        public async Task<IEnumerable<QueuedOrders>> GetAndDrainAllQueuedOrders(DateTime orderDate)
        {
            var orders = _dbContext.QueuedOrders.ToList();

            foreach (var order in orders)
            {
                _dbContext.Entry(order).State = EntityState.Deleted;
            }

            await _dbContext.SaveChangesAsync();

            return orders;
        }

        public async Task<IEnumerable<QueuedOrders>> GetAndDrainQueuedOrders(IEnumerable<int> orderIds, DateTime orderDate)
        {
            var orders = await GetQueuedOrders(orderIds, orderDate);

            foreach (var order in orders)
            {
                _dbContext.Entry(order).State = EntityState.Deleted;
            }

            await _dbContext.SaveChangesAsync();

            return orders;
        }

        public async Task<IEnumerable<TradeLots>> GetTradeLots(Accounts account, string symbol)
        {
            var position = await CheckAndGetExistingPosition(account, symbol);

            var tradeLots = await _dbContext.TradeLots.Where(x => x.AccountId == account.Id && x.PositionId == position.Id).ToListAsync();

            return tradeLots;
        }

        public async Task CreateTradeLot(Positions position, RDSEnums.TradeSide tradeSide, bool isLiquidatingPosition, double tradePrice, double tradeQuantity)
        {
            var tradeLot = new TradeLots()
            {
                AccountId = position.AccountId,
                EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                IsLiquidatingPosition = isLiquidatingPosition,
                PositionId = position.Id,
                TradeDate = DateTime.Today,
                TradePrice = tradePrice,
                TradeQuantity = tradeQuantity,
                TradeSide = tradeSide
            };

            _dbContext.TradeLots.Add(tradeLot);

            await _dbContext.SaveChangesAsync();
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

        public async Task<Accounts> GetAccountUsingId(int accountId)
        {
            return _dbContext.Accounts.FirstOrDefault(x => x.Id == accountId);
        }
    }
}
