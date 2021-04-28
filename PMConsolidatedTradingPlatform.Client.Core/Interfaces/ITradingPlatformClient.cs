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

        void DrainOpenOrders(IEnumerable<int> orderIds, DateTime date);

        void CancelOpenOrders(IEnumerable<int> orderIds, DateTime date);

        void DrainAllOpenOrders(DateTime date);

        void CancelAllOpenOrders(DateTime date);
    }
}
