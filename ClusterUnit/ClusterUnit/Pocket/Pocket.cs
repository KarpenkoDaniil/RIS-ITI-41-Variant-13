using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClusterUnit.NetPocket
{
    public class Pocket
    {
        public int PartOfMesage;
        public byte[] PartOfData;

        public Pocket() { }
        public Pocket(int partOfMesage, byte[] partOfData)
        {
            PartOfData = partOfData;
            PartOfMesage = partOfMesage;
        }
    }
}
