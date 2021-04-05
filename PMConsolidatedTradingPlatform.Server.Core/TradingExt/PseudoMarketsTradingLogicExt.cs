﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingExt
{
    public static class PseudoMarketsTradingLogicExt
    {
        public static bool DoesAccountHaveExistingPosition(this DbSet<Positions> positionsTable, Accounts account,
            string symbol)
        {
            return positionsTable.Any(x => x.AccountId == account.Id && x.Symbol == symbol);
        }

        public static Positions GetExistingPositionFor(this DbSet<Positions> positionsTable, Accounts account,
            string symbol)
        {
            return positionsTable.FirstOrDefault(x => x.AccountId == account.Id && x.Symbol == symbol);
        }

        public static bool IsPositionLong(this Positions position)
        {
            return position.Quantity > 0;
        }

        public static bool IsLiquidatingPosition(this Positions existingPosition, int orderQuantity)
        {
            return existingPosition.Quantity - orderQuantity == 0;
        }
    }
}
