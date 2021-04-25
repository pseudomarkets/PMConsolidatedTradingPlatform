using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace PMConsolidatedTradingPlatform.Client.Core.Models
{
    [MessagePackObject]
    public class OrderDrainerMessage
    {
        [Key(0)] public DateTime Date { get; set; }

        [Key(1)] public bool IsOrderCanceled { get; set; }

        [Key(2)] public IEnumerable<int> OrderIds { get; set; }
    }
}
