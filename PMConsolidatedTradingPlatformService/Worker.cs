using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMConsolidatedTradingPlatform.Server.Core.Logging;
using PMConsolidatedTradingPlatform.Server.Core.Models;
using PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Implementations;

namespace PMConsolidatedTradingPlatformService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<TradingPlatformLogger> _logger;
        private readonly PseudoMarketsTradingService _pmTradingService;
        
        public Worker(ILogger<TradingPlatformLogger> logger)
        {
            if (logger == null)
            {
                ILoggerFactory loggerFactory = new LoggerFactory();

                _logger = loggerFactory.CreateLogger<TradingPlatformLogger>();
            }
            else
            {
                _logger = logger;
            }

            var location = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            var directoryPath = Path.GetDirectoryName(location);

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(directoryPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();

            _pmTradingService = new PseudoMarketsTradingService(configBuilder, _logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _pmTradingService.ProcessIncomingOrder();
            }
        }
    }
}
