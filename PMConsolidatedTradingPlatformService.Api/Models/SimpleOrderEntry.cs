using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PMConsolidatedTradingPlatformService.Api.Models
{
    public class SimpleOrderEntry
    {
        public int AccountId { get; set; }

        public bool EnforceMarketOpenCheck { get; set; }

        public string Symbol { get; set; }

        public int Quantity { get; set; }

        public string OrderAction { get; set; }
    }
}
