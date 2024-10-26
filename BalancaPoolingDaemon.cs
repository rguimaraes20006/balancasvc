using System;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using balancasvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace biex.insumos.balancasvc
{
    public class BalancaPoolingDaemon : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<BalancaDaemonConfig> _config;
        private readonly IOptions<BalancaDaemonAuthentication> _auth;
        private readonly Task _completedTask = Task.CompletedTask;


        public BalancaPoolingDaemon(ILogger<BalancaPoolingDaemon> logger, IOptions<BalancaDaemonConfig> config,
            IOptions<BalancaDaemonAuthentication> auth)
        {
            _logger = logger;
            _config = config;
            _auth = auth;
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Inicializando daemon balança: {ValueIdBalanca} porta: {ValuePorta} API: {ValueApiUrl} ModoTeste: {ValueModoTeste} ",
                _config.Value.id_balanca, _config.Value.Porta, _config.Value.APIUrl, _config.Value.ModoTeste);


            while (!cancellationToken.IsCancellationRequested)
            {
                ProcessAssync(cancellationToken);
                Thread.Sleep(_config.Value.RefreshRate);
            }

            return _completedTask;
        }


        private Task ProcessAssync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Instanciando a porta serial");
            var objPortaSerial = new SerialPort(_config.Value.Porta, 9600, Parity.None, 8, StopBits.One);

            try
            {
                //tenta se conectar com a balança...
                try
                {
                    _logger.LogDebug("Tentando abrir a porta serial");
                    objPortaSerial.Open();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Não conectado a balança. {ExMessage}",
                        ex.Message);
                    return _completedTask;
                }

                var buffer = objPortaSerial.ReadExisting();
                if (buffer == null || buffer.Length == 0)
                {
                    _logger.LogError("Não há medida no buffer da balança");
                    return _completedTask;
                }

                var arrMedidas = buffer.Split('\n');
                var ultimaLinha = arrMedidas[arrMedidas.Length - 2];

                _logger.LogInformation("Medida obtida: {Medida}", buffer);
                float med = 0f;

                //obtém a medida
                try
                {
                    var match = Regex.Match(ultimaLinha, @"([-+]?[0-9]*\.?[0-9]+)");
                    med = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Não foi possível obter a medida do valor : {Indata} erro {ExMessage} ",
                        ultimaLinha, ex.Message);
                }

                try
                {
                    EnviarMedida(new MedidaViewmodel
                    {
                        DataMedicao = DateTime.Now,
                        id_balanca = _config.Value.id_balanca,
                        Valor = med
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao enviar medida {Medida}  para a API: {ExMessage}", med, ex.Message);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Erro ao processar a balança: {ExMessage}, próxima tentativa em: {Tempo}s",
                    e.Message, _config.Value.RefreshRate);
            }
            finally
            {
                objPortaSerial.Close();
                objPortaSerial.Dispose();
            }


            return Task.CompletedTask;
        }

        private Task EnviarMedida(MedidaViewmodel medida)
        {
            _logger.LogInformation("Enviando medida para a API: {MedidaValor} ", medida.Valor);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using (var client = new HttpClient(handler))
            {
                client.BaseAddress = new Uri(_config.Value.APIUrl);

                _logger.LogInformation("Url do post: {UrlPost}", client.BaseAddress);

                //add default headers
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "BalancaDaemon");
                //add basic auth
                client.DefaultRequestHeaders.Add("Authorization",
                    "Basic " + Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{_auth.Value.Username}:{_auth.Value.Password}")));

                var response = client.PostAsJsonAsync("medida", medida).Result;

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Medida enviada com sucesso para a API: {MedidaValor}", medida.Valor);
                }
                else
                {
                    _logger.LogError(
                        "Não foi possível enviar a medida para a API: {MedidaValor} erro: {ResponseStatusCode}  ",
                        medida.Valor, response.StatusCode);
                }
            }

            return _completedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Parando daemon STOP ASYNC");
            return _completedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Desalocando recursos");
        }
    }
}