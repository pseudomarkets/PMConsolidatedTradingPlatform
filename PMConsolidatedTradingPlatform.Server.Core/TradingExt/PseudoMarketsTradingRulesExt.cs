using System;
using System.Collections.Generic;
using System.Text;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingExt
{
    public static class PseudoMarketsTradingRulesExt
    {
        public static bool IsOrderValid(this ConsolidatedTradeRequest tradeRequest)
        {
            return ((tradeRequest?.Quantity > 0 && (tradeRequest?.OrderAction == "BUY" || tradeRequest?.OrderAction == "SELL")) || tradeRequest?.Quantity < 0 && tradeRequest?.OrderAction == "SHORTSELL") &&
                   tradeRequest?.OrderType == ConsolidatedTradeEnums.ConsolidatedOrderType.Limit ||
                   tradeRequest?.OrderType == ConsolidatedTradeEnums.ConsolidatedOrderType.Market ||
                   tradeRequest?.OrderType == ConsolidatedTradeEnums.ConsolidatedOrderType.Stop ||
                   tradeRequest?.OrderType == ConsolidatedTradeEnums.ConsolidatedOrderType.StopLimit &&
                   tradeRequest?.OrderTiming == ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly ||
                   tradeRequest?.OrderTiming == ConsolidatedTradeEnums.ConsolidatedOrderTiming.AfterHours ||
                   tradeRequest?.OrderAction == "BUY" || tradeRequest?.OrderAction == "SELL" ||
                   tradeRequest?.OrderAction == "SELLSHORT";
        }

        public static bool DoesAccountHaveSufficientFundsFor(this Accounts account, int quantity, double price)
        {
            var orderValue = quantity * price;
            return account?.Balance >= orderValue;
        }
    }
}
