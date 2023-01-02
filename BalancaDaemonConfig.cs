

namespace biex.insumos
{
    public class BalancaDaemonConfig
    {
        public int id_balanca { get; set; }        
        public string Porta { get; set; }
        public string APIUrl { get; set; }
        public bool ModoTeste { get; set; }
        public int RefreshRate { get; set; }
    }
}
