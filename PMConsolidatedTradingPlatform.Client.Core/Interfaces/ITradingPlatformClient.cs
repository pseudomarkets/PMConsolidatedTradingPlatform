using System;
using System.Collections.Generic;
using System.Text;
using PMCommonEntities.Models.TradingPlatform;

namespace PMConsolidatedTradingPlatform.Client.Core.Interfaces
{
    public interface ITradingPlatformClient
    {
        void SendTradeRequest(ConsolidatedTradeRequest tradeRequest);

        ConsolidatedTradeResponse GetTradeResponse();

        void Disconnect();
    }
}
