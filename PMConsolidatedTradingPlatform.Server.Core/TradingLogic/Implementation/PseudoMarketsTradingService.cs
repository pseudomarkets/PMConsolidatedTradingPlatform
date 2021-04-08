using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Implementation;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Implementation;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.TradingExt;
using PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Implementation
{
    public class PseudoMarketsTradingService : IPseudoMarketsTradingLogic
    {
        private MarketDataServiceClient _marketDataServiceClient;
        private NetMqService _netMqService;
        private RelationalDataStoreRepository _relationalDataStoreRepository;
        private ConsolidatedTradeEnums.DataStore _dataStore;

        public PseudoMarketsTradingService(IConfigurationRoot config)
        {
            _netMqService = new NetMqService(config.GetSection("NetMqConfig:PseudoMarketsMq")?.Value);
            _marketDataServiceClient = new MarketDataServiceClient(new HttpClient(),
                config.GetSection("MarketDataService:Username")?.Value,
                config.GetSection("MarketDataService:Password")?.Value,
                config.GetSection("MarketDataService:BaseUrl")?.Value);

            _dataStore =
                Enum.Parse<ConsolidatedTradeEnums.DataStore>(config.GetSection("ServiceConfig:OrderPosting")
                    ?.Value); 

            ConfigureServices(new ServiceCollection(), config.GetSection("RelationalDataStore:PseudoMarketsDb")?.Value, _dataStore);

        }

        private void ConfigureServices(IServiceCollection services, string connectionString, ConsolidatedTradeEnums.DataStore dataStore)
        {
            switch (dataStore)
            {
                case ConsolidatedTradeEnums.DataStore.Legacy:
                    services.AddDbContext<PseudoMarketsDbContext>(options => options.UseSqlServer(connectionString));

                    var serviceProvider = services.BuildServiceProvider();
                    var context = serviceProvider.GetService<PseudoMarketsDbContext>();

                    services.AddScoped<IRelationalDataStoreRepository, RelationalDataStoreRepository>(rds =>
                        new RelationalDataStoreRepository(context));
                    break;
                case ConsolidatedTradeEnums.DataStore.RealTime:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataStore), dataStore, null);
            }
        }

        public async void ProcessIncomingOrder()
        {
            var inboundOrder = _netMqService.GetMessage<ConsolidatedTradeRequest>();
            
            switch (inboundOrder.OrderOrigin)
            {
                case ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets:
                    var orderResult = await ProcessPseudoMarketsOrder(inboundOrder);
                    PostOrderResult(orderResult);
                    break;
                case ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoXchange:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<ConsolidatedTradeResponse> ProcessPseudoMarketsOrder(ConsolidatedTradeRequest order)
        {
            switch (_dataStore)
            {
                case ConsolidatedTradeEnums.DataStore.Legacy:
                    var orderResult = await ProcessAndPostLegacyOrder(order);
                    return orderResult;
                    break;
                case ConsolidatedTradeEnums.DataStore.RealTime:
                    return default;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<ConsolidatedTradeResponse> ProcessAndPostLegacyOrder(ConsolidatedTradeRequest order)
        {
            ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

            if (order.IsOrderValid())
            {
                var account = order.Account;

                if (account != null)
                {
                    var quote = await _marketDataServiceClient.GetLatestPrice(order?.Symbol);
                    if (account.DoesAccountHaveSufficientFundsFor(order.Quantity, quote.price))
                    {
                        var transaction = await _relationalDataStoreRepository.CreateTransaction(account.Id,
                            RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets);

                        await _relationalDataStoreRepository.CreateOrder(order.Symbol, order.OrderAction,
                            quote.price, order.Quantity, DateTime.Now, transaction.TransactionId,
                            RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets,
                            RDSEnums.SecurityType.RealWorld);

                        var orderFromTransactionId =
                            await _relationalDataStoreRepository.GetOrderUsingTransactionId(transaction.TransactionId);

                        switch (order.OrderAction)
                        {
                            case "BUY":
                                var buySideResult = await ProcessBuySideLegacyTransaction(account, orderFromTransactionId);
                                response.Order = buySideResult.Order;
                                response.StatusCode = buySideResult.StatusCode;
                                response.StatusMessage = buySideResult.Status;
                                return response;
                        }
                    }
                    else
                    {
                        response.StatusMessage = StatusMessages.InsufficientBalanceMessage;
                        response.StatusCode = TradeStatusCodes.ExecutionError;
                    }
                }
            }

            return response;
        }

        private async Task<(Orders Order, string Status, TradeStatusCodes StatusCode)> ProcessBuySideLegacyTransaction(Accounts account, Orders order)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var orderValue = order.CalculateOrderMarketValue();

            if (existingPosition != null)
            {
                // Existing Long Position
                if (existingPosition.IsPositionLong())
                {
                    var newValue = existingPosition.Value + orderValue;
                    var newQuantity = existingPosition.Quantity + order.Quantity;

                    var newBalance = account.Balance - orderValue;

                    await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                        account, newBalance);

                    return (order, StatusMessages.SuccessMessage, TradeStatusCodes.ExecutionOk);
                }
                
                // Existing Short Position
                if (existingPosition.IsPositionShort())
                {
                    // Liquidating short position
                    if (Math.Abs(existingPosition.Quantity) == order.Quantity)
                    {
                        var gainOrLoss = existingPosition.Value - orderValue;
                        var newBalance = account.Balance += gainOrLoss;

                        await _relationalDataStoreRepository.LiquidatePosition(existingPosition, account, newBalance);
                    }
                    // Buying back shares to reduce stake in short position
                    else
                    {
                        var gainOrLoss = existingPosition.Value - orderValue;
                        var newBalance = account.Balance += gainOrLoss;
                        var newQuantity = existingPosition.Quantity + order.Quantity;

                        await _relationalDataStoreRepository.UpdatePosition(existingPosition, gainOrLoss, newQuantity,
                            account, newBalance);
                    }
                    
                    return (order, StatusMessages.SuccessMessage, TradeStatusCodes.ExecutionOk);
                }
                
                // Invalid order 
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (null, StatusMessages.InvalidOrderTypeMessage, TradeStatusCodes.ExecutionError);
            }
            
            // New position
            Positions position = new Positions
            {
                AccountId = account.Id,
                OrderId = order.Id,
                Value = orderValue,
                Symbol = order.Symbol,
                Quantity = order.Quantity,
                EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                OriginId = RDSEnums.OriginId.PseudoMarkets,
                SecurityTypeId = RDSEnums.SecurityType.RealWorld
            };

            var balance = account.Balance - orderValue;

            await _relationalDataStoreRepository.CreatePosition(position, account, balance);

            return (order, StatusMessages.SuccessMessage, TradeStatusCodes.ExecutionOk);
        }

        private async Task<(Orders Order, string Status, TradeStatusCodes StatusCode)> ProcessSellSideLegacyTransaction(Accounts account, Orders order)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var marketValue = order.CalculateOrderMarketValue();
            
            // Existing position
            if (existingPosition != null)
            {
                // Liquidating long position
                if (existingPosition.IsLiquidatingPosition(order.Quantity))
                {
                    var balance = account.Balance + marketValue;
                    await _relationalDataStoreRepository.LiquidatePosition(existingPosition, account, balance);

                    return (order, StatusMessages.SuccessMessage, TradeStatusCodes.ExecutionOk);
                }
                
                // Sell shares to reduce stake in long position
                var newValue = existingPosition.Value -= marketValue;
                var newQuantity = existingPosition.Quantity -= order.Quantity;
                var newBalance = account.Balance + marketValue;

                await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                    account, newBalance);

                return (order, StatusMessages.SuccessMessage, TradeStatusCodes.ExecutionOk);
            }
            // Invalid order if no position exists
            else
            {
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (null, $"{StatusMessages.InvalidPositionsMessage}{order.Symbol}", TradeStatusCodes.ExecutionError);
            }
        }

        private async Task<(Orders Order, string Status, TradeStatusCodes StatusCode)>  ProcessShortSellSideLegacyTransaction(Accounts account, Orders order)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var marketValue = order.CalculateOrderMarketValue();

            if (existingPosition != null)
            {
                
            }

            return default;
        }

        public void PostOrderResult(ConsolidatedTradeResponse tradeResponse)
        {
            switch (_dataStore)
            {
                case ConsolidatedTradeEnums.DataStore.Legacy:
                    break;
                case ConsolidatedTradeEnums.DataStore.RealTime:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            throw new NotImplementedException();
        }
    }
}
