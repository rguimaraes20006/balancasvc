using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using balancasvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;

namespace biex.insumos.balancasvc
{
    public class BalancaDaemon : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<BalancaDaemonConfig> _config;
        private readonly IOptions<BalancaDaemonAuthentication> _auth;

        public SerialPort objPortaSerial;
        private Task _asyncTask;


        public BalancaDaemon(ILogger<BalancaDaemon> logger, IOptions<BalancaDaemonConfig> config,
            IOptions<BalancaDaemonAuthentication> auth)
        {
            _logger = logger;
            _config = config;
            _auth = auth;
        }

        public Task TestAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Gerando medida de teste");

                EnviarMedida(new MedidaViewmodel
                {
                    DataMedicao = DateTime.Now,
                    id_balanca = _config.Value.id_balanca,
                    Valor = 0.7f * (new Random()).Next(0, 33)
                });

                Thread.Sleep(_config.Value.RefreshRate);
            }

            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"Inicializando daemon balança: {_config.Value.id_balanca} porta: {_config.Value.Porta} API: {_config.Value.APIUrl} ModoTeste: {_config.Value.ModoTeste} ");

            if (_config.Value.ModoTeste)
            {
                TestAsync(cancellationToken);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Instanciando a porta serial");

            objPortaSerial = new SerialPort(_config.Value.Porta, 9600, Parity.None, 8, StopBits.One);
            objPortaSerial.DataReceived += objPortaSerial_DataReceivedAsync;


            //caso não conecte na balança ele tenta novamente após alguns segundos 
            while (cancellationToken.IsCancellationRequested == false && !objPortaSerial.IsOpen)
            {
                try
                {
                    _logger.LogInformation("Tentando abrir a porta serial");
                    //tenta abrir a porta 
                    objPortaSerial.Open();
                    _logger.LogInformation("Porta aberta");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Não conectado a balança. Tentando novamente em 10s Erro: {ex.Message}");
                    //aguarda 10 segundos
                    Thread.Sleep(10000);
                }
            }

            _asyncTask = CarregaMedidaBalanca(cancellationToken);

            while (cancellationToken.IsCancellationRequested == false)
            {
                Task.Delay(_config.Value.RefreshRate, cancellationToken);
            }

            return Task.CompletedTask;
        }

        private async Task CarregaMedidaBalanca(CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Chamou carga medida balanca");

            //enviar o comando SI 
            await Task.Run(() =>
            {
                _logger.LogDebug($"Enviando comando SI para a balança");

                String command = "SI" + Environment.NewLine;
                byte[] asciiByte = System.Text.Encoding.ASCII.GetBytes(command);
                objPortaSerial.Write(asciiByte, 0, asciiByte.Length);
                _logger.LogDebug($"Comando SI enviado");
            }, cancellationToken);
        }

        float med_atual = -10f;

        private async void objPortaSerial_DataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            
            float med = 0f;
            SerialPort sp = (SerialPort) sender;
            string indata = sp.ReadExisting();

            //obtém a medida
            try
            {
                var match = Regex.Match(indata, @"([-+]?[0-9]*\.?[0-9]+)");
                med = float.Parse(match.Groups[1].Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Não foi possível obter a medida do valor : {indata} erro {ex.Message} ");
            }

            if (med != med_atual)
            {
                //envia a medida para a API
                MedidaViewmodel medida = new MedidaViewmodel();
                medida.DataMedicao = DateTime.Now;
                medida.id_balanca = _config.Value.id_balanca;
                medida.Valor = med;
                await EnviarMedida(medida);
            }
        }

        public Task EnviarMedida(MedidaViewmodel medida)
        {
            var client = new RestClient(_config.Value.APIUrl);

            client.Authenticator =
                new RestSharp.Authenticators.NtlmAuthenticator($"{_auth.Value.Username}", _auth.Value.Password);

            var request = new RestRequest("medida/", Method.POST);
            request.RequestFormat = DataFormat.Json;

            var body = new MedidaViewmodel
            {
                DataMedicao = medida.DataMedicao,
                id_balanca = medida.id_balanca,
                Valor = medida.Valor
            };

            request.AddJsonBody(body);

            var result = client.Execute(request);

            if ( !result.IsSuccessful)
            {
                _logger.LogError($"A resposta do serviço foi: {result.StatusCode} - {result.StatusDescription} ");
            }
            else
            {
                _logger.LogInformation($"Medida enviada com sucesso: {medida.Valor} ");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            objPortaSerial.Close();

            _logger.LogInformation("Parando daemon.");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Desalocando recursos....");
        }
    }
}