using System;
using System.Collections.Generic;
using System.Text;
using PMConsolidatedTradingPlatform.Server.Core.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces
{
    public interface IPseudoMarketsTradingLogic
    {
        void ProcessIncomingOrder();
        public void PostOrderResult(ConsolidatedTradeResponse tradeResponse);
    }
}
