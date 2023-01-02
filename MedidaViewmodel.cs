using System;
using System.Collections.Generic;
using System.Text;

namespace balancasvc
{
    public class MedidaViewmodel
    {
        public int id_balanca { get; set; }
        public System.DateTime DataMedicao { get; set; }
        public float Valor { get; set; }

        public override string ToString()
        {
            return $"{DataMedicao} - {id_balanca} - {Valor}";
        }
    }

}
