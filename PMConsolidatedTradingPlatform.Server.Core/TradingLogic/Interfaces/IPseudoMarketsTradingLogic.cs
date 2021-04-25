using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Server.Core.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces
{
    public interface IPseudoMarketsTradingLogic
    {
        Task ProcessIncomingOrder();
        public void PostOrderResult(ConsolidatedTradeResponse tradeResponse);
    }
}
