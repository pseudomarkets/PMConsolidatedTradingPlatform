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
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.TradingExt;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Implementation
{
    public class PseudoMarketsTradingService : IPseudoMarketsTradingLogic
    {
        private MarketDataServiceClient _marketDataServiceClient;
        private NetMqService _netMqService;
        private RelationalDataStoreRepository _relationalDataStoreRepository;
        private ConsolidatedTradeEnums.OrderPostingSystem _orderPostingSystem;

        public PseudoMarketsTradingService(IConfigurationRoot config)
        {
            _netMqService = new NetMqService(config.GetSection("NetMqConfig:PseudoMarketsMq")?.Value);
            _marketDataServiceClient = new MarketDataServiceClient(new HttpClient(),
                config.GetSection("MarketDataService:Username")?.Value,
                config.GetSection("MarketDataService:Password")?.Value,
                config.GetSection("MarketDataService:BaseUrl")?.Value);

            _orderPostingSystem =
                Enum.Parse<ConsolidatedTradeEnums.OrderPostingSystem>(config.GetSection("ServiceConfig:OrderPosting")
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

            switch (_orderPostingSystem)
            {
                case ConsolidatedTradeEnums.OrderPostingSystem.Legacy:
                    break;
                case ConsolidatedTradeEnums.OrderPostingSystem.RealTime:
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
                var account = await _relationalDataStoreRepository.GetAccountUsingId(order.AccountId);

                if (account != null)
                {
                    var quote = await _marketDataServiceClient.GetLatestPrice(order?.Symbol);
                    if (account.DoesAccountHaveSufficientFundsFor(order.Quantity, quote.price))
                    {

                    }
                }
            }

            return response;
        }


        public void PostOrderResult(ConsolidatedTradeResponse tradeResponse)
        {
            switch (_orderPostingSystem)
            {
                case ConsolidatedTradeEnums.OrderPostingSystem.Legacy:
                    break;
                case ConsolidatedTradeEnums.OrderPostingSystem.RealTime:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            throw new NotImplementedException();
        }
    }
}
