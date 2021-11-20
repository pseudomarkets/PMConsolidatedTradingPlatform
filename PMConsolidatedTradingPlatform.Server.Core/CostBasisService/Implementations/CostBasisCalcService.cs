using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PMCommonEntities.Models;
using PMConsolidatedTradingPlatform.Server.Core.CostBasisService.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.CostBasisService.Implementations
{
    public class CostBasisCalcService : ICostBasisCalcService
    {
        private readonly IRelationalDataStoreRepository _relationalDataStore;

        public CostBasisCalcService(IRelationalDataStoreRepository relationalDataStore)
        {
            _relationalDataStore = relationalDataStore;
        }

        public async Task<double> GetAverageCostBasis(Accounts account, string symbol)
        {
            var tradeLots = await _relationalDataStore.GetTradeLots(account, symbol);

            double averageCost = 0;
            int numberOfTrades = 0;

            if (tradeLots != null && tradeLots.Any())
            {
                foreach (var tradeLot in tradeLots)
                {
                    if (tradeLot.TradeSide == RDSEnums.TradeSide.BuySide)
                    {
                        averageCost += tradeLot.TradePrice * tradeLot.TradeQuantity;
                        numberOfTrades++;
                    }
                }
            }

            return (averageCost / numberOfTrades);
        }

        public double CalculateTradeValueUsingFifoCostBasis(IEnumerable<TradeLots> tradeLots, double sellCost)
        {
            var fifoLots = tradeLots.OrderBy(x => x.TradeDate).ToList();

            double fifoCostBasis = 0;

            foreach (var tradeLot in fifoLots)
            {
                if (tradeLot.TradeSide == RDSEnums.TradeSide.BuySide)
                {
                    fifoCostBasis += sellCost - (tradeLot.TradeQuantity * tradeLot.TradePrice);
                }
            }

            return fifoCostBasis;
        }
    }
}
