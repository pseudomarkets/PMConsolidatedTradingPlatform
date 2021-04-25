using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Server.Core.Helpers;
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
        private readonly ILogger<TradingPlatformLogger> _logger;

        public PseudoMarketsTradingService(IConfigurationRoot config, ILogger<TradingPlatformLogger>? logger)
        {
            if (logger == null)
            {
                ILoggerFactory loggerFactory = new LoggerFactory();

                _logger = loggerFactory.CreateLogger<TradingPlatformLogger>();
            }
            else
            {
                _logger = logger;
            }

            try
            {
                if (config == null)
                {
                    var location = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                    var directoryPath = Path.GetDirectoryName(location);

                    var configBuilder = new ConfigurationBuilder()
                        .SetBasePath(directoryPath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();

                    config = configBuilder;
                }

                var tradingPlatformConfig = config.GetSection("TradingPlatformConfig");
                _netMqService = new NetMqService(tradingPlatformConfig.GetSection("NetMqConfig:PseudoMarketsMq")?.Value);
                _marketDataServiceClient = new MarketDataServiceClient(new HttpClient(),
                    tradingPlatformConfig.GetSection("MarketDataServiceConfig:Username")?.Value,
                    tradingPlatformConfig.GetSection("MarketDataServiceConfig:Password")?.Value,
                    tradingPlatformConfig.GetSection("MarketDataServiceConfig:BaseUrl")?.Value);

                _dataStore =
                    Enum.Parse<ConsolidatedTradeEnums.DataStore>(tradingPlatformConfig.GetSection("ServiceConfig:OrderPosting")
                        ?.Value);

                var services = ConfigureServices(new ServiceCollection(), tradingPlatformConfig.GetSection("RelationalDataStore:PseudoMarketsDb")?.Value, _dataStore);

                var context = services.GetService<PseudoMarketsDbContext>();

                _relationalDataStoreRepository = new RelationalDataStoreRepository(context);

                _logger.LogInformation("Services configured");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not start Trading Service");
            }
        }

        private ServiceProvider ConfigureServices(IServiceCollection services, string connectionString, ConsolidatedTradeEnums.DataStore dataStore)
        {
            ServiceProvider serviceProvider = default;

            try
            {
                switch (dataStore)
                {
                    case ConsolidatedTradeEnums.DataStore.Legacy:
                        services.AddDbContext<PseudoMarketsDbContext>(options => options.UseSqlServer(connectionString));

                        serviceProvider = services.BuildServiceProvider();

                        _logger.LogInformation($"Connected to Relational Data Store via connection {connectionString}");

                        break;
                    case ConsolidatedTradeEnums.DataStore.RealTime:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dataStore), dataStore, null);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not configure services");
            }

            return serviceProvider;
        }

        public async Task ProcessIncomingOrder()
        {
            try
            {
                var inboundOrder = _netMqService?.GetMessage<ConsolidatedTradeRequest>();
            
                _logger.LogInformation($"Processing incoming order from origin {inboundOrder?.OrderOrigin}");
            
                switch (inboundOrder?.OrderOrigin)
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
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing inbound order from MQ");
            }

        }

        private async Task<ConsolidatedTradeResponse> ProcessPseudoMarketsOrder(ConsolidatedTradeRequest order)
        {
            try
            {
                switch (_dataStore)
                {
                    case ConsolidatedTradeEnums.DataStore.Legacy:
                        _logger.LogInformation(
                            $"Processing Order via Legacy Posting for account ID: {order?.AccountId}");
                        var orderResult = await ProcessAndPostLegacyOrder(order);
                        return orderResult;
                    case ConsolidatedTradeEnums.DataStore.RealTime:
                        return default;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing Pseudo Markets order");
                return default;
            }
        }

        private async Task<ConsolidatedTradeResponse> ProcessAndPostLegacyOrder(ConsolidatedTradeRequest order)
        {
            ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();
            
            try
            {
                if (order != null && order.IsOrderValid())
                {
                    var account = await _relationalDataStoreRepository.GetAccountUsingId(order.AccountId);

                    if (account != null)
                    {
                        var isMarketHoliday = await _relationalDataStoreRepository.MarketHolidayCheck();
                        var quote = await _marketDataServiceClient.GetLatestPrice(order?.Symbol);

                        if (MarketOpenCheckHelper.IsMarketOpen(isMarketHoliday))
                        {
                            
                            if (account.DoesAccountHaveSufficientFundsFor(order.Quantity, quote.price))
                            {
                                var transaction = await _relationalDataStoreRepository.CreateTransaction(account.Id,
                                    RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets);

                                _logger.LogInformation($"Transaction Created with ID: {transaction.TransactionId}");

                                await _relationalDataStoreRepository.CreateOrder(order.Symbol, order.OrderAction,
                                    quote.price, order.Quantity, DateTime.Now, transaction.TransactionId,
                                    RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets,
                                    RDSEnums.SecurityType.RealWorld);

                                _logger.LogInformation($"Order inserted into Relational Data Store");

                                var orderFromTransactionId =
                                    await _relationalDataStoreRepository.GetOrderUsingTransactionId(transaction.TransactionId);

                                switch (order.OrderAction)
                                {
                                    case "BUY":
                                        _logger.LogInformation("Processing buy side order");
                                        var buySideResult = await ProcessBuySideLegacyTransaction(account, orderFromTransactionId);
                                        response.Order = buySideResult.Order;
                                        response.StatusCode = buySideResult.StatusCode;
                                        response.StatusMessage = buySideResult.Status;
                                        return response;
                                    case "SELL":
                                        _logger.LogInformation("Processing sell side order");
                                        var sellSideResult =
                                            await ProcessSellSideLegacyTransaction(account, orderFromTransactionId);
                                        response.Order = sellSideResult.Order;
                                        response.StatusCode = sellSideResult.StatusCode;
                                        response.StatusMessage = sellSideResult.Status;
                                        return response;
                                    case "SELLSHORT":
                                        _logger.LogInformation("Processing short-sell side order");
                                        var shortSellSideResult =
                                            await ProcessShortSellSideLegacyTransaction(account, orderFromTransactionId);
                                        response.Order = shortSellSideResult.Order;
                                        response.StatusCode = shortSellSideResult.StatusCode;
                                        response.StatusMessage = shortSellSideResult.Status;
                                        return response;
                                    default:
                                        _logger.LogError($"Invalid order action {order.OrderAction}");
                                        response.Order = default;
                                        response.StatusMessage = StatusMessages.InvalidOrderTypeMessage;
                                        response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                                        return response;
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Market is closed, creating queued order now");
                                await _relationalDataStoreRepository.CreateQueuedOrder(order.Symbol, order.OrderAction,
                                    order.Quantity, RDSEnums.EnvironmentId.ProductionPrimary, true, DateTime.Today,
                                    order.AccountId);
                                response.StatusMessage = "Market is closed, order has been queued to be filled on next market open";
                                response.Order = default;
                                return response;
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Insufficient balance for Account ID: {account.Id} with balance of {account.Balance} and order value of {order.Quantity * quote.price}");
                            response.StatusMessage = StatusMessages.InsufficientBalanceMessage;
                            response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                            response.Order = default;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Failed to get account");
                        response.StatusMessage = "Invalid account";
                        response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                        response.Order = default;
                    }
                }
                else
                {
                    _logger.LogInformation("Trade rules validation failed for order");
                    response.StatusMessage = $"{StatusMessages.InvalidSymbolOrQuantityMessage} and/or {StatusMessages.InvalidOrderTypeMessage}";
                    response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                    response.Order = default;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in processing legacy order and transaction posting");
                response.Order = default;
                response.StatusMessage = StatusMessages.FailureMessage;
                response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                
            }
            
            return response;
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)> ProcessBuySideLegacyTransaction(Accounts account, Orders order)
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

                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
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
                    
                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                
                // Invalid order 
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (default, StatusMessages.InvalidOrderTypeMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
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

            return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)> ProcessSellSideLegacyTransaction(Accounts account, Orders order)
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

                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                
                // Sell shares to reduce stake in long position
                var newValue = existingPosition.Value -= marketValue;
                var newQuantity = existingPosition.Quantity -= order.Quantity;
                var newBalance = account.Balance + marketValue;

                await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                    account, newBalance);

                return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
            }
            // Invalid order if no position exists
            else
            {
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (default, $"{StatusMessages.InvalidPositionsMessage}{order.Symbol}", ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
            }
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)>  ProcessShortSellSideLegacyTransaction(Accounts account, Orders order)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var marketValue = order.CalculateOrderMarketValue();

            if (existingPosition != null)
            {
                if (existingPosition.IsPositionLong())
                {
                    await _relationalDataStoreRepository.DeleteInvalidOrder(order);

                    _logger.LogInformation($"Cannot initiate short on existing long position; position ID:  {existingPosition?.Id}");

                    return (default, StatusMessages.InvalidShortPositionMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
                }
                else if (existingPosition.IsPositionShort())
                {
                    var newValue = existingPosition.Value + marketValue;
                    var newQuantity = existingPosition.Quantity + (Math.Abs(order.Quantity) * -1);

                    var newBalance = account.Balance - marketValue;

                    await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity, account,
                        newBalance);

                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                else
                {
                    // Invalid order type if short-selling against an existing position that is not long or short
                    return (default, StatusMessages.InvalidOrderTypeMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
                }
            } // Create new short position
            else
            {
                Positions newPosition = new Positions()
                {
                    AccountId = account.Id,
                    OrderId = order.Id,
                    Symbol = order.Symbol,
                    Quantity = Math.Abs(order.Quantity) * -1,
                    EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                    OriginId = RDSEnums.OriginId.PseudoMarkets,
                    SecurityTypeId = RDSEnums.SecurityType.RealWorld
                };

                var newBalance = account.Balance - marketValue;
                await _relationalDataStoreRepository.CreatePosition(newPosition, account, newBalance);

                return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
            }
        }

        public void PostOrderResult(ConsolidatedTradeResponse tradeResponse)
        {
            switch (_dataStore)
            {
                case ConsolidatedTradeEnums.DataStore.Legacy:
                    _logger.LogInformation($"Posting trade response from Legacy Posting for Order ID: {tradeResponse?.Order?.Id}");
                    _netMqService.SendMessage(tradeResponse);
                    _logger.LogInformation($"Trade response sent for Order ID: {tradeResponse?.Order?.Id}");
                    break;
                case ConsolidatedTradeEnums.DataStore.RealTime:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
