namespace Cluster.PocketOfData
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
