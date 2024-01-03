using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net;
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
    public class BalancaDaemon : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<BalancaDaemonConfig> _config;
        private readonly IOptions<BalancaDaemonAuthentication> _auth;

        // public SerialPort objPortaSerial;

        public BalancaDaemon(ILogger<BalancaDaemon> logger, IOptions<BalancaDaemonConfig> config,
            IOptions<BalancaDaemonAuthentication> auth)
        {
            _logger = logger;
            _config = config;
            _auth = auth;
        }

        private Task TestAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Gerando medida de teste");

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

        private  Task ProcessSerialRealtime(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Instanciando a porta serial");

            var objBalanca = new SerialPort(_config.Value.Porta, 9600, Parity.None, 8, StopBits.One);
            //conexão com a balança
            int qtdTentativas = 0;
            while (cancellationToken.IsCancellationRequested == false && !objBalanca.IsOpen)
            {
                if (qtdTentativas > 3)
                {
                    _logger.LogError("Não foi possível conectar a balança. Saindo do daemon");
                    break;
                }

                try
                {
                    _logger.LogInformation("Tentando abrir a porta serial");
                    //tenta abrir a porta 
                    objBalanca.Open();
                    _logger.LogInformation("Porta aberta conectado a balança enviando comando SI");
                    String command = "SI" + Environment.NewLine;
                    byte[] asciiByte = System.Text.Encoding.ASCII.GetBytes(command);
                    objBalanca.Write(asciiByte, 0, asciiByte.Length);
                    _logger.LogInformation("Comando SI enviado");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Não conectado a balança. Tentando novamente em 10s Erro: {ExMessage}",
                        ex.Message);
                    //aguarda 10 segundos
                    Thread.Sleep(10000);
                }

                qtdTentativas++;
            }


            float _medAtual = 0f;
            
            //loop infinito enquanto n rolar o ctrl+c e a balança estiver aberta
            while (cancellationToken.IsCancellationRequested == false && objBalanca.IsOpen)
            {
                //obtendo a medida atual da balança
                var arrMedidas = objBalanca.ReadExisting().Split('\n');
                var ultimaLinha = arrMedidas[arrMedidas.Length - 2];
                
                
                float med = 0f;

                //obtém a medida
                try
                {
                    var match = Regex.Match(ultimaLinha, @"([-+]?[0-9]*\.?[0-9]+)");
                    med = float.Parse(match.Groups[1].Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Não foi possível obter a medida do valor : {Indata} erro {ExMessage} ", ultimaLinha, ex.Message);
                }

                if (med != _medAtual)
                {
                    _logger.LogInformation("Medida recebida: {Med} é diferente da ultima {MedAtual}", med, _medAtual);
                    
                    //envia a medida para a API
                    MedidaViewmodel medida = new MedidaViewmodel();
                    medida.DataMedicao = DateTime.Now;
                    medida.id_balanca = _config.Value.id_balanca;
                    medida.Valor = med;
                    _medAtual = med;
                    EnviarMedida(medida);
                }
                else
                {
                    _logger.LogDebug("Medida recebida: {Med} é igual a ultima {MedAtual}", med, _medAtual);
                }
                
                Thread.Sleep(_config.Value.RefreshRate);
                
            }
            
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Inicializando daemon balança: {ValueIdBalanca} porta: {ValuePorta} API: {ValueApiUrl} ModoTeste: {ValueModoTeste} ",
                _config.Value.id_balanca, _config.Value.Porta, _config.Value.APIUrl, _config.Value.ModoTeste);

            if (_config.Value.ModoTeste)
            {
                TestAsync(cancellationToken);
                return Task.CompletedTask;
            }
            else
            {
                ProcessSerialRealtime(cancellationToken);
                return Task.CompletedTask;
            }
        }



        /*
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
                _logger.LogError("Não foi possível obter a medida do valor : {Indata} erro {ExMessage} ", indata, ex.Message);
            }

            if (med != _medAtual)
            {
                _logger.LogInformation("Medida recebida: {Med} é diferente da ultima {MedAtual}", med, _medAtual);

                //envia a medida para a API
                MedidaViewmodel medida = new MedidaViewmodel();
                medida.DataMedicao = DateTime.Now;
                medida.id_balanca = _config.Value.id_balanca;
                medida.Valor = med;

                _medAtual = med;
                EnviarMedida(medida);
            }
            else
            {
                _logger.LogDebug("Medida recebida: {Med} é igual a ultima {MedAtual}", med, _medAtual);
            }
        }
        */

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

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
          // objPortaSerial.Close();
            _logger.LogInformation("Parando daemon.");
            return Task.CompletedTask;
        }


        public void Dispose()
        {
            _logger.LogInformation("Desalocando recursos....");
        }
    }
}