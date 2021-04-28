using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PMConsolidatedTradingPlatformService.Api.Models
{
    public class SimpleOrderDrainerRequest
    {
        public List<int> OrderIds { get; set; }

        public string OrderDate { get; set; }
    }
}
