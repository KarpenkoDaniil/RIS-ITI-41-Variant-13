using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            string serverAddress = "127.0.0.1"; // Замените на ваш адрес
            int serverPort = 4000;             // Порт сервера
            string imagePath = AppDomain.CurrentDomain.BaseDirectory + "Images"; // Укажите путь к изображениям

            string[] files = Directory.GetFiles(imagePath);
            Random random = new Random();

            int numberOfRequests = 1000;          // Количество запросов

            try
            {
                // Создаем массив задач для параллельной отправки изображений
                Task[] tasks = new Task[numberOfRequests];

                for (int i = 0; i < numberOfRequests; i++)
                {
                    string imgPath = files[random.Next(files.Length)];
                    tasks[i] = UploadImageAsync(serverAddress, serverPort, imgPath, i + 1);
                    Thread.Sleep(50);
                }

                // Ожидаем завершения всех задач
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        static string pathToLoger = AppDomain.CurrentDomain.BaseDirectory + "LogFile.txt";
        
        static async Task UploadImageAsync(string serverAddress, int serverPort, string imagePath, int requestNumber)
        {
            try
            {
                using var client = new TcpClient();
                DateTime dateTime = DateTime.Now;
                await client.ConnectAsync(serverAddress, serverPort);

                using var stream = client.GetStream();

                // Читаем изображение в массив байтов
                byte[] imageData;
                using (var bitmap = new Bitmap(imagePath))
                {
                    using (var memory = new MemoryStream())
                    {
                        using (var clone = new Bitmap(bitmap.Width, bitmap.Height, bitmap.PixelFormat))
                        {
                            using (var gr = Graphics.FromImage(clone))
                            {
                                gr.DrawImage(bitmap, new Rectangle(0, 0, clone.Width, clone.Height));
                            }
                            clone.Save(memory, ImageFormat.Png);
                        }
                        imageData = memory.ToArray();
                    }
                }

                // Формируем HTTP-запрос
                string boundary = "----WebKitFormBoundaryd7MAyRomwE3UD8Bo";
                string fileName = Path.GetFileName(imagePath);
                string headers = $"POST / HTTP/1.1\r\n" +
                                 $"Host: {serverAddress}:{serverPort}\r\n" +
                                 $"Content-Type: multipart/form-data; boundary={boundary}\r\n" +
                                 $"Content-Length: {imageData.Length}\r\n" +
                                 "Connection: close\r\n" +
                                 "\r\n";

                string bodyHeaders = $"--{boundary}\r\n" +
                                     $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n" +
                                     "Content-Type: image/jpeg\r\n" +
                                     "\r\n";

                string bodyFooter = $"\r\n--{boundary}--\r\n";

                // Отправляем данные
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers + bodyHeaders);
                byte[] footerBytes = Encoding.ASCII.GetBytes(bodyFooter);
                int lenght = 0;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    memoryStream.Write(headerBytes);
                    memoryStream.Write(imageData);
                    memoryStream.Write(footerBytes);

                    lenght = memoryStream.ToArray().Length;

                    await stream.WriteAsync(memoryStream.ToArray(), 0, lenght);
                }

                // Читаем ответ сервера
                var responseBuffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                var AfterdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffff");

                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                string str = "";

                if (lenght < 400)
                {
                    str = "Time: " + AfterdateTime + "; Byte's: " + lenght + "; Bad.\n";
                }
                else
                {
                    str = "Time: " + AfterdateTime + "; Byte's: " + lenght + "; Ok.\n";
                }

                

                File.AppendAllText(pathToLoger, str);
                Console.WriteLine($"Запрос #{requestNumber}: Ответ - {response}");
            }
            catch (Exception ex)
            {
                //string str = "Time: " + (AfterdateTime - dateTime) + "; Byte's: " + lenght + "; Ok.";
                Console.WriteLine($"Ошибка в запросе #{requestNumber}: {ex.Message}");
            }
        }
    }
}