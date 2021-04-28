using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Client.Core.Implementation;
using PMConsolidatedTradingPlatformService.Api.Models;

namespace PMConsolidatedTradingPlatformService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderRouterController : ControllerBase
    {
        private readonly TradingPlatformClient _tradingPlatformClient;

        public OrderRouterController(TradingPlatformClient tradingPlatformClient)
        {
            _tradingPlatformClient = tradingPlatformClient;
        }

        // POST: /api/OrderRouter/SendOrder
        [Route("SendOrder")]
        [HttpPost]
        public async Task<ActionResult> SendOrder([FromBody] SimpleOrderEntry orderEntry)
        {
            try
            {
                ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

                if (orderEntry != null)
                {
                    var tradeRequest = new ConsolidatedTradeRequest()
                    {
                        AccountId = orderEntry.AccountId,
                        EnforceMarketOpenCheck = orderEntry.EnforceMarketOpenCheck,
                        OrderAction = orderEntry.OrderAction,
                        OrderOrigin = ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets,
                        OrderTiming = ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly,
                        OrderType = ConsolidatedTradeEnums.ConsolidatedOrderType.Market,
                        Quantity = orderEntry.Quantity,
                        Symbol = orderEntry.Symbol
                    };

                    _tradingPlatformClient.SendTradeRequest(tradeRequest);

                    response = _tradingPlatformClient.GetTradeResponse();

                    return Ok(response);
                }

                return BadRequest("All fields in Order Entry are required");
            }
            catch (Exception e)
            {
                return StatusCode(500, e);
            }
        }


        // POST: /api/OrderRouter/DrainAllOrders
        [Route("DrainAllOrders")]
        [HttpPost]
        public async Task<ActionResult> DrainAllOrders([FromHeader] string orderDate)
        {
            try
            {
                ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

                if (!string.IsNullOrEmpty(orderDate))
                {
                    DateTime.TryParseExact(orderDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate);

                    _tradingPlatformClient.DrainAllOpenOrders(parsedDate);

                    response = _tradingPlatformClient.GetTradeResponse();

                    return Ok(response);
                }

                return BadRequest("Order Date is required");
            }
            catch (Exception e)
            {
                return StatusCode(500, e);
            }
        }

        // POST: /api/OrderRouter/CancelAllOrders
        [Route("CancelAllOrders")]
        [HttpPost]
        public async Task<ActionResult> CancelAllOrders([FromHeader] string orderDate)
        {
            try
            {
                ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

                if (!string.IsNullOrEmpty(orderDate))
                {
                    DateTime.TryParseExact(orderDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate);

                    _tradingPlatformClient.CancelAllOpenOrders(parsedDate);

                    response = _tradingPlatformClient.GetTradeResponse();

                    return Ok(response);
                }

                return BadRequest("Order Date is required");
            }
            catch (Exception e)
            {
                return StatusCode(500, e);
            }
        }

        // POST: /api/OrderRouter/CancelOrdersByOrderId
        [Route("CancelOrdersByOrderId")]
        [HttpPost]
        public async Task<ActionResult> CancelOrdersByOrderId([FromBody] SimpleOrderDrainerRequest orderDrainer)
        {
            try
            {
                ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

                if (orderDrainer != null)
                {
                    DateTime.TryParseExact(orderDrainer.OrderDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate);

                    _tradingPlatformClient.CancelOpenOrders(orderDrainer.OrderIds, parsedDate);

                    response = _tradingPlatformClient.GetTradeResponse();

                    return Ok(response);
                }

                return BadRequest("Order Drainer request is required");
            }
            catch (Exception e)
            {
                return StatusCode(500, e);
            }
        }

        // POST: /api/OrderRouter/DrainOrdersByOrderId
        [Route("DrainOrdersByOrderId")]
        [HttpPost]
        public async Task<ActionResult> DrainOrdersByOrderId([FromBody] SimpleOrderDrainerRequest orderDrainer)
        {
            try
            {
                ConsolidatedTradeResponse response = new ConsolidatedTradeResponse();

                if (orderDrainer != null)
                {
                    DateTime.TryParseExact(orderDrainer.OrderDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate);

                    _tradingPlatformClient.DrainOpenOrders(orderDrainer.OrderIds, parsedDate);

                    response = _tradingPlatformClient.GetTradeResponse();

                    return Ok(response);
                }

                return BadRequest("Order Drainer request is required");
            }
            catch (Exception e)
            {
                return StatusCode(500, e);
            }
        }
    }
}
