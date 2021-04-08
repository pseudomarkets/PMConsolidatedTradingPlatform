using System;
using System.Collections.Generic;
using System.Text;

namespace PMConsolidatedTradingPlatform.Server.Core.Models
{
    public class ConsolidatedTradeEnums
    {
        public enum ConsolidatedOrderType
        {
            Market, 
            Limit,
            Stop,
            StopLimit
        }

        public enum ConsolidatedOrderTiming
        {
            DayOnly,
            AfterHours
        }

        public enum ConsolidatedOrderOrigin
        {
            PseudoMarkets,
            PseudoXchange
        }

        public enum DataStore
        {
            Legacy,
            RealTime,
            Dual
        }
    }
}
