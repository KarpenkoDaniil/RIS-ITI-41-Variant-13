using System.Net.Sockets;
using System.Text;

namespace HTTP.Claster
{
    public class Claster
    {
        private byte[] _dataRecv;
        private Socket ClasterSocket;
        public bool IsFree => _isFree;
        private bool _isFree = true;
        private object _monitor = new object();

        public Claster(Socket clasterSocket)
        {
            ClasterSocket = clasterSocket;
            ClasterSocket.NoDelay = true;
            ClasterSocket.SendBufferSize = 1024*128;
            Thread thread1 = new Thread(ClasterRecive);
            thread1.Start();
        }

        public async Task<byte[]> SendData(byte[] bytes)
        {
            SendToClasterData(bytes);
            _isFree = false;
            lock (_monitor)
            {
                Monitor.Wait(_monitor);
            }
            _isFree = true;
            return _dataRecv;
        }

        private void ClasterRecive()
        {
            while (true)
            {
                if (ClasterSocket.Available > 0)
                {
                    // Получаем размер данных
                    byte[] sizeBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int read = ClasterSocket.Receive(sizeBuffer, totalRead, 4 - totalRead, SocketFlags.None);
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
                            int read = ClasterSocket.Receive(buffer, 0,
                                Math.Min(buffer.Length, expectedSize - totalRead),
                                SocketFlags.None);

                            if (read == 0) break; // соединение закрыто

                            memoryStream.Write(buffer, 0, read);
                            totalRead += read;
                        }

                        _dataRecv = memoryStream.ToArray();

                        string str = Encoding.UTF8.GetString(_dataRecv);

                        lock (_monitor)
                        {
                            Monitor.PulseAll(_monitor);
                        }
                    }
                }
            }
        }

        private void SendToClasterData(byte[] dataSend)
        {
            Console.WriteLine("Send to claster size of data = " + dataSend.Length);

            // Отправляем размер данных
            byte[] sizeBytes = BitConverter.GetBytes(dataSend.Length);
            ClasterSocket.Send(sizeBytes);

            // Отправляем данные
            int totalSent = 0;
            while (totalSent < dataSend.Length)
            {
                int sent = ClasterSocket.Send(dataSend, totalSent, dataSend.Length - totalSent, SocketFlags.None);
                totalSent += sent;
            }
        }
    }
}
