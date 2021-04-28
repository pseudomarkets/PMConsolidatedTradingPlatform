using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PMConsolidatedTradingPlatformService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AboutController : ControllerBase
    {
        [Route("Service")]
        [HttpGet]
        public ActionResult About()
        {
            return Ok("Pseudo Markets Trading Platform API");
        }
    }
}
