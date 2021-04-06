using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Implementation;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Implementation;
using PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMMarketDataService.DataProvider.Client.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMCommonEntities.Models;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.TradingExt;
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

            ConfigureServices(new ServiceCollection(), config.GetSection("RelationalDataStore:PseudoMarketsDb")?.Value);

        }

        private void ConfigureServices(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<PseudoMarketsDbContext>(options => options.UseSqlServer(connectionString));

            var serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetService<PseudoMarketsDbContext>();

            services.AddScoped<IRelationalDataStoreRepository, RelationalDataStoreRepository>(rds =>
                new RelationalDataStoreRepository(context));
        }

        public void ProcessIncomingOrder()
        {
            var inboundOrder = _netMqService.GetMessage<ConsolidatedTradeRequest>();


            switch (inboundOrder.OrderOrigin)
            {
                case ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets:
                    var orderResult = ProcessPseudoMarketsOrder(inboundOrder);
                    PostOrderResult(orderResult);
                    break;
                case ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoXchange:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ConsolidatedTradeResponse ProcessPseudoMarketsOrder(ConsolidatedTradeRequest order)
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

            return default;
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
                        var transaction = await _relationalDataStoreRepository.CreateAndSaveTransaction(account.Id,
                            RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets);

                        var dbOrder= await _relationalDataStoreRepository.CreateAndSaveOrder(order.Symbol, order.OrderAction,
                            quote.price, order.Quantity, DateTime.Now, transaction.TransactionId,
                            RDSEnums.EnvironmentId.ProductionPrimary, RDSEnums.OriginId.PseudoMarkets,
                            RDSEnums.SecurityType.RealWorld);

                        switch (order.OrderAction)
                        {
                            case "BUY":
                                await ProcessBuySideLegacyTransaction(account, dbOrder);
                                break;
                            default:
                                break;
                        }

                    }
                }
            }

            return response;
        }

        private async Task ProcessBuySideLegacyTransaction(Accounts account, Orders order)
        {
            // Check if account is holding an existing position
            var existingPosition = await _relationalDataStoreRepository.CheckAndGetExistingPosition(account, order.Symbol);

            var marketValue = order.CalculateOrderMarketValue();

            if (existingPosition != null)
            {
                // Existing Long Position
                if (existingPosition.Quantity > 0)
                {
                    var newValue = existingPosition.Value + marketValue;
                    var newQuantity = existingPosition.Quantity + order.Quantity;

                    var newBalance = account.Balance - marketValue;

                    await _relationalDataStoreRepository.UpdatePosition(existingPosition, newValue, newQuantity,
                        account, newBalance);

                }
                // Existing Short Position
                else
                {
                    // Liquidating short position
                    if (Math.Abs(existingPosition.Quantity) == order.Quantity)
                    {

                    }
                    // Increasing stake in short position
                    else
                    {
                        
                    }
                }
            }
            // New position
            else
            {
                
            }
        }

        private async Task ProcessSellSideLegacyTransaction(Accounts account, Orders order)
        {

        }

        private async Task ProcessShortSellSideLegacyTransaction(Accounts account, Orders order)
        {

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
