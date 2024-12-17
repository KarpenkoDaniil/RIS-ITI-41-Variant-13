using ClusterUnit.ComputeTools;
using ClusterUnit.NetPocket;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClusterUnit.UnitClient
{
    public class Unit
    {
        Socket _clasterSocket;
        CompressIMG _compressIMG;
        FastCompressIMG _fastDCTCompressIMG;
        FastnDCTCompress _fastnDCTCompressIMG;

        public Unit()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Configure.json";
            string json = File.ReadAllText(path);

            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            IPEndPoint IP = new IPEndPoint(IPEndPoint.Parse(dictionary["IPadres"]).Address, int.Parse(dictionary["PORT"]));
            _clasterSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IP = new IPEndPoint(IPEndPoint.Parse(dictionary["CLASTERIP"]).Address, int.Parse(dictionary["CLASTERPORT"]));

            while (true)
            {
                try
                {
                    _clasterSocket.Connect(IP);
                    Console.WriteLine("Unit connect to cluster with IP:PORT = " + _clasterSocket.RemoteEndPoint);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(100);
                }
            }

            //_compressIMG = new CompressIMG();
            _fastDCTCompressIMG = new FastCompressIMG();
            //_fastnDCTCompressIMG = new FastnDCTCompress();

            Thread thread = new Thread(Listen);
            thread.Start();
        }

        private async void Listen()
        {
            while (true)
            {
                if (_clasterSocket.Available > 0)
                {
                    // Получаем размер данных
                    byte[] sizeBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int read = _clasterSocket.Receive(sizeBuffer, totalRead, 4 - totalRead, SocketFlags.None);
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
                            int read = _clasterSocket.Receive(buffer, 0,
                                Math.Min(buffer.Length, expectedSize - totalRead),
                                SocketFlags.None);

                            if (read == 0) break; // соединение закрыто

                            memoryStream.Write(buffer, 0, read);
                            totalRead += read;
                        }

                        Console.WriteLine($"Actually received {totalRead} bytes");

                        Pocket pocket = JsonConvert.DeserializeObject<Pocket>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));

                        Console.WriteLine("Unit recive block of data;\n" + "part of block = " + pocket.PartOfMesage + "\n" + "Num of byte's = " + pocket.PartOfData.Length + "\n");

                        DateTime dateTime = DateTime.Now;
                        pocket = new Pocket(pocket.PartOfMesage, _fastDCTCompressIMG.CompressImageAsync(pocket.PartOfData).Result);

                        var time = DateTime.Now - dateTime;
                        Console.WriteLine("\nComplete time: " + time + "\n");
                        string st = JsonConvert.SerializeObject(pocket);
                        var bytes = Encoding.UTF8.GetBytes(st);

                        SendLengthAndData(bytes);
                        Console.WriteLine("Unit send block of data;\n" + "part of block = " + pocket.PartOfMesage + "\n" + "Num of byte's = " + pocket.PartOfData.Length + "\n");
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void SendLengthAndData(byte[] dataSend)
        {
            // Сначала отправляем размер данных
            byte[] sizeBytes = BitConverter.GetBytes(dataSend.Length);
            _clasterSocket.Send(sizeBytes);

            // Затем отправляем сами данные
            int totalSent = 0;
            while (totalSent < dataSend.Length)
            {
                int sent = _clasterSocket.Send(dataSend, totalSent,
                    dataSend.Length - totalSent, SocketFlags.None);
                totalSent += sent;
            }
        }
    }
}
