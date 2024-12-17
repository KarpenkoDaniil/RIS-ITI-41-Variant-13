using System.Net.Sockets;
using System.Text;

namespace HTTP
{
    public class HTTPResponse
    {
        public void SendHtml(NetworkStream stream, string content)
        {
            string response = "HTTP/1.1 200 OK\r\n" +
                              "Content-Type: text/html\r\n" +
                              "Content-Length: " + content.Length + "\r\n" +
                              "\r\n" +
                              content;

            byte[] buffer = Encoding.ASCII.GetBytes(response);
            stream.Write(buffer, 0, buffer.Length);
        }

        public async Task SendHtmlAsync(NetworkStream stream, string content)
        {
            // Преобразуем строку HTML в байты с использованием кодировки UTF-8
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);

            // Создаем HTTP-ответ с заголовками
            string responseHeaders = "HTTP/1.1 200 OK\r\n" +
                                     "Content-Type: text/html; charset=utf-8\r\n" +
                                     "Content-Length: " + contentBytes.Length + "\r\n" +
                                     "\r\n";

            // Преобразуем заголовки в байты
            byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeaders);

            // Отправляем заголовки
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

            // Отправляем контент (сам HTML)
            await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
        }

        public void SendFile(NetworkStream stream, byte[] fileBytes, string contentType)
        {
            string headers = $"HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {fileBytes.Length}\r\n" +
                $"Connection: close\r\n" +  // Добавляем это
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(fileBytes, 0, fileBytes.Length);
        }

        public void SendError(NetworkStream stream, string errorMessage)
        {
            string response = "HTTP/1.1 404 Not Found\r\n" +
                              "Content-Type: text/html\r\n" +
                              "Content-Length: " + errorMessage.Length + "\r\n" +
                              "\r\n" +
                              errorMessage;

            byte[] buffer = Encoding.ASCII.GetBytes(response);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
