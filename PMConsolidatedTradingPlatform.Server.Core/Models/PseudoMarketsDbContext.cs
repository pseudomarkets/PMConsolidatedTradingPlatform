using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.Models
{
    public class PseudoMarketsDbContext : DbContext
    {
        public PseudoMarketsDbContext(DbContextOptions<PseudoMarketsDbContext> options) : base(options)
        {

        }

        public DbSet<Users> Users { get; set; }
        public DbSet<Tokens> Tokens { get; set; }
        public DbSet<ApiKeys> ApiKeys { get; set; }
        public DbSet<Accounts> Accounts { get; set; }
        public DbSet<Positions> Positions { get; set; }
        public DbSet<Orders> Orders { get; set; }
        public DbSet<Transactions> Transactions { get; set; }
        public DbSet<QueuedOrders> QueuedOrders { get; set; }
        public DbSet<MarketHolidays> MarketHolidays { get; set; }
        public DbSet<TradeLots> TradeLots { get; set; }
    }
}
