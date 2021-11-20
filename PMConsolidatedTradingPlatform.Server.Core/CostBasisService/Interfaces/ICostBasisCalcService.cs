using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.CostBasisService.Interfaces
{
    public interface ICostBasisCalcService
    {
        Task<double> GetAverageCostBasis(Accounts account, string symbol);

        double CalculateTradeValueUsingFifoCostBasis(IEnumerable<TradeLots> tradeLots, double sellCost);
    }
}
