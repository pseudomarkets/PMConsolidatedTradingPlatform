using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.Models
{
    [MessagePackObject]
    public class ConsolidatedTradeResponse
    {
        [Key(0)]
        public string StatusMessage { get; set; }

        [Key(1)]
        public TradeStatusCodes StatusCode { get; set; }

        [Key(2)]
        public Orders Order { get; set; }
    }
}
