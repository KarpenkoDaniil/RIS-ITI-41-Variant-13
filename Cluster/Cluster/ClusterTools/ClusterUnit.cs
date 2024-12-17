using Cluster.PocketOfData;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;

namespace Cluster.ClusterTools
{
    public class ClusterUnit
    {
        EventHandler _complete;
        Socket _unitSocet;
        ServerCluster _serverCluster;
        bool _isFree = true;

        public ClusterUnit(Socket socket, ServerCluster serverCluster)
        {
            _unitSocet = socket;
            _serverCluster = serverCluster;
            Thread thread = new Thread(CileOfListen);
            thread.Start();
        }

        //Прием данных с UnitBlock
        private void CileOfListen()
        {
            while (true)
            {
                if (_unitSocet.Available > 0)
                {
                    // Получаем размер данных
                    byte[] sizeBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int read = _unitSocet.Receive(sizeBuffer, totalRead, 4 - totalRead, SocketFlags.None);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    int expectedSize = BitConverter.ToInt32(sizeBuffer, 0);
                    Console.WriteLine($"Expecting to receive {expectedSize} bytes");

                    // Получаем сами данные
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192]; // больший размер буфера для эффективности
                        totalRead = 0;

                        while (totalRead < expectedSize)
                        {
                            int read = _unitSocet.Receive(buffer, 0,
                                Math.Min(buffer.Length, expectedSize - totalRead),
                                SocketFlags.None);

                            if (read == 0) break; // соединение закрыто

                            memoryStream.Write(buffer, 0, read);
                            totalRead += read;
                        }

                        Console.WriteLine($"Actually received {totalRead} bytes");

                        Pocket pocket = JsonConvert.DeserializeObject<Pocket>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));
                        _serverCluster.AddPocket(pocket);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        public void SendDataToUnit(Pocket pocket)
        {
            if (_isFree)
            {
                var dataSend = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pocket));

                // Сначала отправляем размер данных
                byte[] sizeBytes = BitConverter.GetBytes(dataSend.Length);
                _unitSocet.Send(sizeBytes);

                // Затем отправляем сами данные
                int totalSent = 0;
                while (totalSent < dataSend.Length)
                {
                    int sent = _unitSocet.Send(dataSend, totalSent,
                        dataSend.Length - totalSent, SocketFlags.None);
                    totalSent += sent;
                }
            }
        }

    }
}
