using System.Net.Sockets;
using System.Text;

namespace HTTP
{
    public class HTTPRequest
    {
        public string Method;
        public string Path;
        public string Boundary;

        public byte[] HTTPRequestBytes;

        public static HTTPRequest Parse(NetworkStream stream)
        {
            byte[] buffer = new byte[1024 * 4]; // 4 kB buffer
            int bytesRead;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bool avb = true;
                try
                {
                    DateTime lastReceiveTime = DateTime.Now;
                    const int TIMEOUT_MS = 2000; // таймаут 1 секунда

                    while (true)
                    {
                        if (stream.DataAvailable)
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                            memoryStream.Write(buffer, 0, bytesRead);
                            lastReceiveTime = DateTime.Now;
                        }
                        else
                        {
                            // Проверяем таймаут
                            if ((DateTime.Now - lastReceiveTime).TotalMilliseconds > TIMEOUT_MS)
                            {
                                break;
                            }

                            
                        }

                        // Проверка на отключение сокета
                        if (!stream.Socket.Connected)
                        {
                            break;
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine("Ошибка ввода-вывода: " + ioEx.Message);
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("Соединение закрыто");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Произошла ошибка: " + ex.Message);
                }

                HTTPRequest request = new HTTPRequest();
                request.HTTPRequestBytes = memoryStream.ToArray();

                // Преобразуем MemoryStream в строку запроса
                string requestString = Encoding.UTF8.GetString(memoryStream.ToArray());

                // Разбиваем запрос на строки
                string[] lines = requestString.Split(new[] { "\r\n" }, StringSplitOptions.None);

                // Проверяем, что есть хотя бы одна строка для заголовка
                if (lines.Length > 0)
                {
                    string[] requestLineParts = lines[0].Split(' ');

                    if (requestLineParts.Length >= 2)
                    {
                        request.Method = requestLineParts[0];
                        request.Path = requestLineParts[1];
                    }
                }

                // Ищем границу для multipart/form-data
                foreach (string line in lines)
                {
                    if (line.StartsWith("Content-Type: multipart/form-data; boundary="))
                    {
                        request.Boundary = line.Substring(line.IndexOf("boundary=") + 9);
                        break; // Граница найдена, дальнейший поиск не нужен
                    }
                }

                return request;
            }
        }
    }
}
