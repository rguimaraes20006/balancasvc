using System;
using System.Threading;
using System.Threading.Tasks;
using biex.insumos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace balancasvc
{
    public class MachineMetricsDaemon : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<BalancaDaemonConfig> _config;

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MachineMetricsDaemon is running");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                
                Thread.Sleep(5000);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MachineMetricsDaemon is stopping");
            return Task.CompletedTask;
        }
    }
    



   
}