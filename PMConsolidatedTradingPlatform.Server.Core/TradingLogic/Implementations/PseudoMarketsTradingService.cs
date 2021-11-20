using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMCommonEntities.Models.PseudoMarkets;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Server.Core.CostBasisService.Implementations;
using PMConsolidatedTradingPlatform.Server.Core.CostBasisService.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.Helpers;
using PMConsolidatedTradingPlatform.Server.Core.Logging;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Implementations;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Implementations;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Implementations;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Implementations;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMConsolidatedTradingPlatform.Server.Core.TradingExt;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Implementations
{
    public class PseudoMarketsTradingService : IPseudoMarketsTradingLogic
    {
        private MarketDataServiceClient _marketDataServiceClient;
        private NetMqService _netMqService;
        private readonly IRelationalDataStoreRepository _relationalDataStoreRepository;
        private readonly ICostBasisCalcService _costBasisService;
        private ConsolidatedTradeEnums.DataStore _dataStore;
        private readonly ILogger<TradingPlatformLogger> _logger;
        private readonly IRealTimeDbContext _realTimeDbContext;
        private readonly IExtendedTransactionsRepository _extendedTransactionsRepository;
        private readonly string _serviceUser;

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

                var realTimeDataStoreHost = tradingPlatformConfig.GetSection("RealTimeDataStore:Host")?.Value;

                _realTimeDbContext =
                    new RealTimeDbContext(realTimeDataStoreHost,
                        Convert.ToInt32(tradingPlatformConfig.GetSection("RealTimeDataStore:Port")?.Value));

                if (_realTimeDbContext.IsConnected())
                {
                    _logger.LogInformation($"Connected to Real Time Data Store via connection {realTimeDataStoreHost}");
                    _extendedTransactionsRepository = new ExtendedTransactionsRepository(_realTimeDbContext);
                }

                var services = ConfigureServices(new ServiceCollection(), tradingPlatformConfig.GetSection("RelationalDataStore:PseudoMarketsDb")?.Value, _dataStore);

                var context = services.GetService<PseudoMarketsDbContext>();

                _relationalDataStoreRepository = new RelationalDataStoreRepository(context);
                _costBasisService = new CostBasisCalcService(_relationalDataStoreRepository);

                _serviceUser = tradingPlatformConfig.GetSection("ServiceConfig:AppName")?.Value;

                if (string.IsNullOrEmpty(_serviceUser))
                {
                    _serviceUser = "Pseudo Markets Trading Platform Service";
                }

                _logger.LogInformation("Services configured");
                _logger.LogInformation("Ready to accept orders");
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
                if (order != null)
                {
                    if(order.OrderDrainerMessage != null)
                    {
                        var wasDrainerSuccess = await ProcessOrderDrainerMessage(order.OrderDrainerMessage);

                        if (wasDrainerSuccess)
                        {
                            response.StatusMessage = "Order(s) drained";
                            response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk;
                        }
                        else
                        {
                            response.StatusMessage = "Could not drain order(s), check logs for more details";
                            response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                        }

                        response.Order = default;
                        return response;
                    }

                    if (order.IsOrderValid())
                    {
                        var account = await _relationalDataStoreRepository.GetAccountUsingId(order.AccountId);

                        if (account != null)
                        {
                            var isMarketHoliday = await _relationalDataStoreRepository.MarketHolidayCheck();
                            var quote = await _marketDataServiceClient.GetLatestPrice(order?.Symbol);

                            if (order.EnforceMarketOpenCheck)
                            {
                                if (MarketOpenCheckHelper.IsMarketOpen(isMarketHoliday))
                                {
                                    if (account.DoesAccountHaveSufficientFundsFor(order.Quantity, quote.price))
                                    {
                                        response = await CreateTransactionAndProcessOrder(order, account, quote);
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
                                if (account.DoesAccountHaveSufficientFundsFor(order.Quantity, quote.price))
                                {
                                    response = await CreateTransactionAndProcessOrder(order, account, quote);
                                }
                                else
                                {
                                    _logger.LogInformation($"Insufficient balance for Account ID: {account.Id} with balance of {account.Balance} and order value of {order.Quantity * quote.price}");
                                    response.StatusMessage = StatusMessages.InsufficientBalanceMessage;
                                    response.StatusCode = ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError;
                                    response.Order = default;
                                }
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
                else
                {
                    _logger.LogWarning("Order cannot be null");
                    response.StatusMessage = StatusMessages.FailureMessage;
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

        private async Task<ConsolidatedTradeResponse> CreateTransactionAndProcessOrder(ConsolidatedTradeRequest order,
            Accounts account, LatestPriceOutput quote)
        {
            ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

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
                    var buySideResult = await ProcessBuySideLegacyTransaction(account, orderFromTransactionId, transaction.TransactionId, quote.source);
                    response.Order = buySideResult.Order;
                    response.StatusCode = buySideResult.StatusCode;
                    response.StatusMessage = buySideResult.Status;
                    return response;
                case "SELL":
                    _logger.LogInformation("Processing sell side order");
                    var sellSideResult =
                        await ProcessSellSideLegacyTransaction(account, orderFromTransactionId, transaction.TransactionId, quote.source);
                    response.Order = sellSideResult.Order;
                    response.StatusCode = sellSideResult.StatusCode;
                    response.StatusMessage = sellSideResult.Status;
                    return response;
                case "SELLSHORT":
                    _logger.LogInformation("Processing short-sell side order");
                    var shortSellSideResult =
                        await ProcessShortSellSideLegacyTransaction(account, orderFromTransactionId, transaction.TransactionId, quote.source);
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

        private async Task<bool> ProcessOrderDrainerMessage(OrderDrainerMessage drainerMessage)
        {
            try
            {
                if (drainerMessage.ProcessAllOrders)
                {
                    var orders = await _relationalDataStoreRepository.GetAndDrainAllQueuedOrders(drainerMessage.Date);

                    if (!drainerMessage.IsOrderCanceled)
                    {
                        _logger.LogInformation("Draining all orders");
                        foreach (var order in orders)
                        {
                            ConsolidatedTradeRequest tradeRequest = new ConsolidatedTradeRequest()
                            {
                                AccountId = order.UserId,
                                EnforceMarketOpenCheck = false,
                                OrderAction = order.OrderType,
                                OrderOrigin = ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets,
                                OrderTiming = ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly,
                                Quantity = order.Quantity,
                                Symbol = order.Symbol,
                                OrderType = ConsolidatedTradeEnums.ConsolidatedOrderType.Market
                            };

                            await ProcessAndPostLegacyOrder(tradeRequest);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Cancelling all orders");
                        await _relationalDataStoreRepository.CancelAllQueuedOrders(drainerMessage.Date);
                    }
                }
                else
                {
                    var orders = await
                        _relationalDataStoreRepository.GetAndDrainQueuedOrders(drainerMessage.OrderIds, drainerMessage.Date);

                    if (!drainerMessage.IsOrderCanceled)
                    {
                        _logger.LogInformation("Draining select orders");
                        foreach (var order in orders)
                        {
                            ConsolidatedTradeRequest tradeRequest = new ConsolidatedTradeRequest()
                            {
                                AccountId = order.UserId,
                                EnforceMarketOpenCheck = false,
                                OrderAction = order.OrderType,
                                OrderOrigin = ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets,
                                OrderTiming = ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly,
                                Quantity = order.Quantity,
                                Symbol = order.Symbol,
                                OrderType = ConsolidatedTradeEnums.ConsolidatedOrderType.Market
                            };

                            await ProcessAndPostLegacyOrder(tradeRequest);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Cancelling select orders");
                        await _relationalDataStoreRepository.CancelQueuedOrders(drainerMessage.OrderIds,
                            drainerMessage.Date);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while draining orders");
                return false;
            }
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)> ProcessBuySideLegacyTransaction(Accounts account, Orders order, string transactionId, string source)
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

                    var startingBalance = account.Balance;

                    var newBalance = account.Balance - orderValue;

                    await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                        account, newBalance);

                    await _relationalDataStoreRepository.CreateTradeLot(existingPosition, RDSEnums.TradeSide.BuySide,
                        false, order.Price, order.Quantity);

                    // Extended transaction data
                    var extendedTransaction = new ExtendedTransaction()
                    {
                        TransactionId = transactionId,
                        Symbol = order.Symbol,
                        TradeSide = RDSEnums.TradeSide.BuySide,
                        Quantity = order.Quantity,
                        ExecutionPrice = order.Price,
                        SecurityType = RDSEnums.SecurityType.RealWorld,
                        OriginId = RDSEnums.OriginId.PseudoMarkets,
                        AccountId = account.Id,
                        ExecutionTimestamp = DateTime.Now,
                        ServiceUser = _serviceUser,
                        CreditOrDebit = "DEBIT",
                        TransactionType = TransactionType.BuySideRealWorldEquityTransaction,
                        TransactionDescription = $"EXISTING LONG POS BUY SIDE TRAN",
                        AccountStartingBalance = startingBalance,
                        AccountEndingBalance = newBalance,
                        IsTradingTransaction = true,
                        MarketDataSource = source
                    };

                    _extendedTransactionsRepository.UpsertTransaction(extendedTransaction);

                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                
                // Existing Short Position
                if (existingPosition.IsPositionShort())
                {
                    // Liquidating short position
                    if (Math.Abs(existingPosition.Quantity) == order.Quantity)
                    {
                        var gainOrLoss = existingPosition.Value - orderValue;
                        var startingBalance = account.Balance;
                        var newBalance = account.Balance += gainOrLoss;

                        await _relationalDataStoreRepository.LiquidatePosition(existingPosition, account, newBalance);

                        await _relationalDataStoreRepository.CreateTradeLot(existingPosition,
                            RDSEnums.TradeSide.ShortSellSide, true, order.Price, order.Quantity);

                        // Extended transaction data
                        var extendedTransaction = new ExtendedTransaction()
                        {
                            TransactionId = transactionId,
                            Symbol = order.Symbol,
                            TradeSide = RDSEnums.TradeSide.BuySide,
                            Quantity = order.Quantity,
                            ExecutionPrice = order.Price,
                            SecurityType = RDSEnums.SecurityType.RealWorld,
                            OriginId = RDSEnums.OriginId.PseudoMarkets,
                            AccountId = account.Id,
                            ExecutionTimestamp = DateTime.Now,
                            ServiceUser = _serviceUser,
                            CreditOrDebit = "CREDIT",
                            TransactionType = TransactionType.BuySideRealWorldEquityTransaction,
                            TransactionDescription = $"LIQ SHORT POS BUY SIDE TRAN",
                            AccountStartingBalance = startingBalance,
                            AccountEndingBalance = newBalance,
                            IsTradingTransaction = true,
                            MarketDataSource = source
                        };

                        _extendedTransactionsRepository.UpsertTransaction(extendedTransaction);

                    }
                    // Buying back shares to reduce stake in short position
                    else
                    {
                        var gainOrLoss = existingPosition.Value - orderValue;
                        var startingBalance = account.Balance;
                        var newBalance = account.Balance += gainOrLoss;
                        var newQuantity = existingPosition.Quantity + order.Quantity;

                        await _relationalDataStoreRepository.UpdatePosition(existingPosition, gainOrLoss, newQuantity,
                            account, newBalance);

                        await _relationalDataStoreRepository.CreateTradeLot(existingPosition,
                            RDSEnums.TradeSide.BuySide, false, order.Price, order.Quantity);

                        // Extended transaction data
                        var extendedTransaction = new ExtendedTransaction()
                        {
                            TransactionId = transactionId,
                            Symbol = order.Symbol,
                            TradeSide = RDSEnums.TradeSide.BuySide,
                            Quantity = order.Quantity,
                            ExecutionPrice = order.Price,
                            SecurityType = RDSEnums.SecurityType.RealWorld,
                            OriginId = RDSEnums.OriginId.PseudoMarkets,
                            AccountId = account.Id,
                            ExecutionTimestamp = DateTime.Now,
                            ServiceUser = _serviceUser,
                            CreditOrDebit = "CREDIT",
                            TransactionType = TransactionType.BuySideRealWorldEquityTransaction,
                            TransactionDescription = $"REDUCE SHARES SHORT POS BUY SIDE TRAN",
                            AccountStartingBalance = account.Balance,
                            AccountEndingBalance = newBalance,
                            IsTradingTransaction = true,
                            MarketDataSource = source
                        };

                        _extendedTransactionsRepository.UpsertTransaction(extendedTransaction);
                    }
                    
                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                
                // Invalid order 
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (default, StatusMessages.InvalidOrderTypeMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
            }
            else
            {
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

                var startingBalance = account.Balance;
                var balance = account.Balance - orderValue;

                await _relationalDataStoreRepository.CreatePosition(position, account, balance);

                await _relationalDataStoreRepository.CreateTradeLot(position, RDSEnums.TradeSide.BuySide, false, order.Price,
                    order.Quantity);

                // Extended transaction data
                var extendedTransaction = new ExtendedTransaction()
                {
                    TransactionId = transactionId,
                    Symbol = order.Symbol,
                    TradeSide = RDSEnums.TradeSide.BuySide,
                    Quantity = order.Quantity,
                    ExecutionPrice = order.Price,
                    SecurityType = RDSEnums.SecurityType.RealWorld,
                    OriginId = RDSEnums.OriginId.PseudoMarkets,
                    AccountId = account.Id,
                    ExecutionTimestamp = DateTime.Now,
                    ServiceUser = _serviceUser,
                    CreditOrDebit = "DEBIT",
                    TransactionType = TransactionType.BuySideRealWorldEquityTransaction,
                    TransactionDescription = $"NEW LONG POS BUY SIDE TRAN",
                    AccountStartingBalance = startingBalance,
                    AccountEndingBalance = balance,
                    IsTradingTransaction = true,
                    MarketDataSource = source
                };

                _extendedTransactionsRepository.UpsertTransaction(extendedTransaction);

                return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
            }
            
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)> ProcessSellSideLegacyTransaction(Accounts account, Orders order, string transactionId, string source)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var marketValue = order.CalculateOrderMarketValue();
            
            // Existing position
            if (existingPosition != null)
            {
                var tradeLots = await _relationalDataStoreRepository.GetTradeLots(account, existingPosition.Symbol);

                // Liquidating long position
                if (existingPosition.IsLiquidatingPosition(order.Quantity))
                {
                    double balance = 0;

                    if (tradeLots != null && tradeLots.Any())
                    {
                        balance =  account.Balance + _costBasisService.CalculateTradeValueUsingFifoCostBasis(tradeLots, order.CalculateOrderMarketValue());
                    }
                    else
                    {
                        balance = account.Balance + marketValue;
                    }

                    await _relationalDataStoreRepository.LiquidatePosition(existingPosition, account, balance);

                    await _relationalDataStoreRepository.CreateTradeLot(existingPosition, RDSEnums.TradeSide.SellSide,
                        true, order.Price, order.Quantity);

                    return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
                }
                
                // Sell shares to reduce stake in long position
                var newValue = existingPosition.Value -= marketValue;
                var newQuantity = existingPosition.Quantity -= order.Quantity;

                double newBalance = 0;

                if (tradeLots != null && tradeLots.Any())
                {
                    newBalance = account.Balance + _costBasisService.CalculateTradeValueUsingFifoCostBasis(tradeLots, order.CalculateOrderMarketValue());
                }
                else
                {
                    newBalance = account.Balance + marketValue;
                }

                await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                    account, newBalance);

                await _relationalDataStoreRepository.CreateTradeLot(existingPosition, RDSEnums.TradeSide.SellSide,
                    false, order.Price, order.Quantity);

                return (order, StatusMessages.SuccessMessage, ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk);
            }
            // Invalid order if no position exists
            else
            {
                await _relationalDataStoreRepository.DeleteInvalidOrder(order);
                return (default, $"{StatusMessages.InvalidPositionsMessage}{order.Symbol}", ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError);
            }
        }

        private async Task<(Orders Order, string Status, ConsolidatedTradeEnums.TradeStatusCodes StatusCode)>  ProcessShortSellSideLegacyTransaction(Accounts account, Orders order, string transactionId, string source)
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
