﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace biex.insumos.balancasvc
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder().ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", false, true);
            }).ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();
                services.Configure<BalancaDaemonConfig>(hostContext.Configuration.GetSection("Servico"));
                services.Configure<BalancaDaemonAuthentication>(hostContext.Configuration.GetSection("Autenticacao"));
                services.AddHostedService<BalancaPoolingDaemon>();

            }).ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
            });

            //force process end when CTRL+C is pressed
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
            };
            
            await builder.RunConsoleAsync();
        }
    }
}