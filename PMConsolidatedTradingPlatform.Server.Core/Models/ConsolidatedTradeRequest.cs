using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.Models
{
    [MessagePackObject()]
    public class ConsolidatedTradeRequest
    {
        [Key(0)]
        public string Symbol { get; set; }

        [Key(1)]
        public int Quantity { get; set; }

        [Key(2)]
        public string OrderAction { get; set; }

        [Key(3)]
        public ConsolidatedTradeEnums.ConsolidatedOrderType OrderType { get; set; }

        [Key(4)]
        public ConsolidatedTradeEnums.ConsolidatedOrderTiming OrderTiming { get; set; }

        [Key(5)]
        public ConsolidatedTradeEnums.ConsolidatedOrderOrigin OrderOrigin { get; set; }

        [Key(6)]
        public Accounts Account { get; set; }
    }
}
