using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.OpenApi.Models;
using PMConsolidatedTradingPlatform.Client.Core.Implementation;

namespace PMConsolidatedTradingPlatformService.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Trading Platform client
            services.AddSingleton<TradingPlatformClient>(
                new TradingPlatformClient(Configuration.GetValue<string>("ServiceConfig:TradingServiceMq")));

            // Add basic auth
            services.AddAuthentication(IISDefaults.AuthenticationScheme);

            services.AddControllers();

            // Add Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Pseudo Trading Platform Service API",
                    Description = "Web API for interfacing with Consolidated Trading Platform Service",
                    Contact = new OpenApiContact
                    {
                        Name = "Shravan Jambukesan",
                        Email = "shravan@shravanj.com",
                        Url = new Uri("https://shravanj.com"),
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://github.com/pseudomarkets/PMConsolidatedTradingPlatform/blob/master/LICENSE.txt"),
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pseudo Markets Portfolio Performance API");
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
