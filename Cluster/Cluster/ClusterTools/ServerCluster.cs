using Cluster.PocketOfData;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;

namespace Cluster.ClusterTools
{
    public class ServerCluster
    {
        List<ClusterUnit> _clusterUnits = new List<ClusterUnit>();
        List<Pocket> _pocketsToSend = new List<Pocket>();
        List<Pocket> _pocketsToRecive = new List<Pocket>();
        int sizeOfPockets => _clusterUnits.Count;

        //Для общения с ClusterUnit
        Socket _serverClusterSocket;
        //Для общения с сервером
        Socket _serverSocket;

        public ServerCluster()
        {
            string pathToConfig = AppDomain.CurrentDomain.BaseDirectory + "MetaData\\Configure.json";
            string json = File.ReadAllText(pathToConfig);
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            //Создания сокета кластера, для общения кластера и сокета
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint IP = new IPEndPoint(IPEndPoint.Parse(dictionary["IPadres"]).Address, int.Parse(dictionary["PORT"]));
            _serverSocket.Bind(IP);
            IP = new IPEndPoint(IPEndPoint.Parse(dictionary["SERVERIP"]).Address, int.Parse(dictionary["SERVERPORT"]));   

            while (true)
            {
                try
                {
                    _serverSocket.Connect(IP);
                    Console.WriteLine("Cluster connect to server with IP:PORT = " + _serverSocket.LocalEndPoint);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }

            //Присоеденение к серверу кластера вычислительных блоков
            IP = new IPEndPoint(IPEndPoint.Parse(dictionary["SERVERUNITIP"]).Address, int.Parse(dictionary["SERVERUNITPORT"]));
            _serverClusterSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverClusterSocket.Bind(IP);
            _serverClusterSocket.Listen(12);
            Console.WriteLine("Cluster start listen on IP:PORT = " + _serverClusterSocket.LocalEndPoint);

            Thread threadCluster = new Thread(ClasterListener);
            threadCluster.Start();

            Thread threadServer = new Thread(CycleOfServer);
            threadServer.Start();
        }

        private void CycleOfServer()
        {
            while (true)
            {
                if (_serverSocket.Available > 0)
                {
                    // Получаем размер данных
                    byte[] sizeBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int read = _serverSocket.Receive(sizeBuffer, totalRead, 4 - totalRead, SocketFlags.None);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    int expectedSize = BitConverter.ToInt32(sizeBuffer, 0);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(sizeBuffer); // Переворачиваем для big-endian
                    }
                    Console.WriteLine($"Expecting to receive {expectedSize} bytes");

                    // Получаем сами данные
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192]; // больший размер буфера для эффективности
                        totalRead = 0;

                        while (totalRead < expectedSize)
                        {
                            int read = _serverSocket.Receive(buffer, 0,
                                Math.Min(buffer.Length, expectedSize - totalRead),
                                SocketFlags.None);

                            if (read == 0) break; // соединение закрыто

                            memoryStream.Write(buffer, 0, read);
                            totalRead += read;
                        }

                        Console.WriteLine($"Actually received {totalRead} bytes");

                        if (totalRead == expectedSize)
                        {
                            memoryStream.Position = 0; // сбрасываем позицию в начало
                            DesideTask(memoryStream);
                        }
                        else
                        {
                            Console.WriteLine("Error: Incomplete data received");
                        }
                    }
                }
            }
        }

        //Разделение изображения на части 
        private void DesideTask(MemoryStream memoryStream)
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            try
            {
                using (Bitmap bitmap = new Bitmap(memoryStream))
                {
                    int partHeight = bitmap.Height / _clusterUnits.Count;

                    for (int i = 0; i < _clusterUnits.Count; i++)
                    {
                        using (Bitmap partBitmap = bitmap.Clone(new Rectangle(0, i * partHeight, bitmap.Width, partHeight), bitmap.PixelFormat))
                        {
                            // Конвертируем часть изображения в массив байтов
                            byte[] imageBytes;

                            using (MemoryStream ms = new MemoryStream())
                            {
                                partBitmap.Save(ms, ImageFormat.Png);
                                imageBytes = ms.ToArray();
                            }

                            _pocketsToSend.Add(new Pocket(i, imageBytes));
                        }
                    }

                    SendImageToUnits();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _serverSocket.Send(new byte[0]);
            }
        }

        //Отправка изображений на блоки вычисления
        private void SendImageToUnits()
        {
            for (int i = 0; i < _clusterUnits.Count; i++)
            {
                _clusterUnits[i].SendDataToUnit(_pocketsToSend[i]);
            }
            Console.WriteLine("Send to unit's block of IMG, num of block's = " + _clusterUnits.Count);
            _pocketsToSend.Clear();
        }

        //Прием изображеня из блоков вычисления
        private void RecivePocketsFromUnits(byte[] dataSend)
        {
            // Сначала отправляем размер данных
            byte[] sizeBytes = BitConverter.GetBytes(dataSend.Length);
            _serverSocket.Send(sizeBytes);

            // Затем отправляем сами данные
            int totalSent = 0;
            while (totalSent < dataSend.Length)
            {
                int sent = _serverSocket.Send(dataSend, totalSent,
                    dataSend.Length - totalSent, SocketFlags.None);
                totalSent += sent;
            }
        }

        //Обработка пакетов.
        public void AddPocket(Pocket pocket)
        {
            _pocketsToRecive.Add(pocket);
            if (_pocketsToRecive.Count == _clusterUnits.Count)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    List<Bitmap> bitmaps = new List<Bitmap>();
                    int index = 0;
                    int addedParts = 0;
                    while (true)
                    {
                        foreach (var poc in _pocketsToRecive)
                        {
                            if (index == poc.PartOfMesage)
                            {
                                using (MemoryStream memory = new MemoryStream(poc.PartOfData))
                                {
                                    bitmaps.Add(new Bitmap(memory));
                                }
                                //memoryStream.Write(poc.PartOfData);
                                index++;
                                break;
                            }
                        }
                        if (index == _pocketsToRecive.Count)
                        {
                            break;
                        }
                    }

                    int height = bitmaps.Sum(x => x.Height);
                    int width = bitmaps[0].Width;

                    Bitmap combinedBitmap = new Bitmap(width, height);

                    using (Graphics g = Graphics.FromImage(combinedBitmap))
                    {
                        // Очищаем фон (например, делаем его белым)
                        g.Clear(Color.White);
                        int offsetY = 0;

                        foreach (var item in bitmaps)
                        {
                            g.DrawImage(item, 0, offsetY); // Рисуем текущее изображение
                            offsetY += item.Height;
                        }
                    }

                    combinedBitmap.Save(memoryStream, ImageFormat.Jpeg);
                    RecivePocketsFromUnits(memoryStream.ToArray());

                    combinedBitmap.Dispose();

                    foreach (var item in bitmaps)
                    {
                        item.Dispose();
                    }

                    _pocketsToRecive.Clear();
                }
            }
        }

        private void ClasterListener()
        {
            while (true)
            {
                var unitSocket = _serverClusterSocket.Accept();
                _clusterUnits.Add(new ClusterUnit(unitSocket, this));
                Console.WriteLine("Connect to cluster unit block with IP:PORT = " + unitSocket.RemoteEndPoint);
            }
        }
    }
}
