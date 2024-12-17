using HTTP;
using System.Net.Sockets;
using System.Text;

public static class FileHandler
{
    public static async Task<byte[]> HandleUpload(HTTPRequest request)
    {
        string uploadDirectory = AppDomain.CurrentDomain.BaseDirectory + "uploads";

        // Создание директории для загрузки файлов, если ее нет
        if (!Directory.Exists(uploadDirectory))
        {
            Directory.CreateDirectory(uploadDirectory);
        }

        byte[] buffer = new byte[1024];
        using (MemoryStream memoryStream = new MemoryStream())
        {
            // Преобразуем прочитанные байты в строку
            byte[] requestBytes = request.HTTPRequestBytes;
            string requestString = Encoding.UTF8.GetString(requestBytes);

            // 1. Поиск заголовков
            int contentStart = requestString.IndexOf("\r\n\r\n") + 4;
            if (contentStart == 3) // IndexOf вернул -1, добавление +4 дало 3
                throw new InvalidOperationException("HTTP headers not properly terminated.");

            string headers = requestString.Substring(0, contentStart);

            // 2. Извлечение boundary
            string boundary = ExtractBoundaryFromHeaders(headers);
            if (string.IsNullOrEmpty(boundary))
                throw new InvalidOperationException("Boundary not found in Content-Type header.");

            // Добавляем "--" к началу boundary для корректного поиска
            string fullBoundary = boundary.Replace("\"", "");

            // 3. Поиск имени файла
            string contentDispositionHeader = "Content-Disposition:";
            int contentDispositionIndex = requestString.IndexOf(contentDispositionHeader);
            if (contentDispositionIndex == -1)
                throw new InvalidOperationException("Content-Disposition header not found.");

            int filenameStart = requestString.IndexOf("filename=\"", contentDispositionIndex);
            if (filenameStart == -1)
                throw new InvalidOperationException("Filename not found in Content-Disposition header.");

            filenameStart += 10; // Пропускаем "filename=\""
            int filenameEnd = requestString.IndexOf("\"", filenameStart);
            if (filenameEnd == -1)
                throw new InvalidOperationException("Invalid filename format.");

            string fileName = requestString.Substring(filenameStart, filenameEnd - filenameStart);
            if (fileName == "")
            {
                throw new InvalidDataException("Empty image.");
            }
            Console.WriteLine($"Extracted filename: {fileName}");

            // 4. Поиск начала данных файла
            int fileDataStart = requestString.IndexOf("\r\n\r\n", contentDispositionIndex) + 4;
            if (fileDataStart == 3)
                throw new InvalidOperationException("File data not properly terminated.");

            // 5. Поиск следующего boundary
            int boundaryIndex = requestString.IndexOf(fullBoundary, fileDataStart);
            if (boundaryIndex == -1)
                throw new InvalidOperationException("Boundary not found in request body.");

            // Поиск заголовка Content-Length
            string contentLengthHeader = "Content-Length:";
            int contentLengthIndex = requestString.IndexOf(contentLengthHeader);

            if (contentLengthIndex == -1)
                throw new InvalidOperationException("Content-Length header not found.");

            // Индекс начала числа в заголовке
            int contentLengthStart = contentLengthIndex + contentLengthHeader.Length;
            int contentLengthEnd = requestString.IndexOf("\r\n", contentLengthStart);

            if (contentLengthEnd == -1)
                contentLengthEnd = requestString.Length; // если это последний заголовок

            // Извлекаем значение Content-Length и парсим его
            string contentLengthStr = requestString.Substring(contentLengthStart, contentLengthEnd - contentLengthStart).Trim();
            if (!int.TryParse(contentLengthStr, out int contentLength))
                throw new InvalidOperationException("Invalid Content-Length value.");

            // Удаляем "\r\n" перед boundary
            int fileDataLength = int.Parse(contentLengthStr); // Учитываем \r\n перед boundary
            if (fileDataLength < 0)
                throw new InvalidOperationException("Invalid file data length.");

            byte[] actualFileBytes = requestBytes.Skip(fileDataStart).Take(fileDataLength).ToArray();
            Console.WriteLine("\nActualy Recived from client " + actualFileBytes.Length +"\n");

            return actualFileBytes;
        }
    }

    private static string ExtractBoundaryFromHeaders(string headers)
    {
        int boundaryIndex = headers.IndexOf("boundary=");
        if (boundaryIndex > -1)
        {
            int boundaryStart = boundaryIndex + 9; // длина строки "boundary="
            int boundaryEnd = headers.IndexOf("\r\n", boundaryStart);
            return headers.Substring(boundaryStart, boundaryEnd - boundaryStart);
        }

        return null;
    }

    public static void SendFile(NetworkStream stream, string path)
    {
        string filePath = Path.Combine(Environment.CurrentDirectory, path.TrimStart('/'));
        if (File.Exists(filePath))
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            HTTPResponse response = new HTTPResponse();
            string contentType = GetContentType(filePath);
            response.SendFile(stream, fileBytes, contentType);
        }
        else
        {
            HTTPResponse response = new HTTPResponse();
            response.SendError(stream, "404 Not Found");
        }
    }

    public static string GetHtmlForm()
    {
        return "<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>Image Upload Form</title>\r\n    <script src=\"https://cdn.tailwindcss.com\"></script>\r\n    <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.3/css/all.min.css\"></link>\r\n    <link href=\"https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap\" rel=\"stylesheet\">\r\n    <style>\r\n        body {\r\n            font-family: 'Roboto', sans-serif;\r\n        }\r\n        .file-input::-webkit-file-upload-button {\r\n            visibility: hidden;\r\n        }\r\n        .file-input::before {\r\n            content: 'Select Image';\r\n            display: inline-block;\r\n            background: linear-gradient(to right, #4f46e5, #3b82f6);\r\n            border: 1px solid #4f46e5;\r\n            border-radius: 0.375rem;\r\n            padding: 0.5rem 1rem;\r\n            outline: none;\r\n            white-space: nowrap;\r\n            cursor: pointer;\r\n            color: white;\r\n            font-weight: 700;\r\n            font-size: 0.875rem;\r\n            transition: background 0.3s ease;\r\n        }\r\n        .file-input:hover::before {\r\n            background: linear-gradient(to right, #3b82f6, #4f46e5);\r\n        }\r\n        .file-input:active::before {\r\n            background: linear-gradient(to right, #3b82f6, #4f46e5);\r\n        }\r\n    </style>\r\n</head>\r\n<body class=\"bg-gray-100 flex items-center justify-center min-h-screen\">\r\n    <div class=\"bg-white p-8 rounded-lg shadow-lg w-full max-w-md\">\r\n        <h2 class=\"text-2xl font-bold mb-6 text-center\">Upload Your Image</h2>\r\n        <form action=\"\\\" method=\"POST\" enctype=\"multipart/form-data\" class=\"space-y-6\">\r\n            <div>\r\n                <label for=\"image\" class=\"block text-sm font-medium text-gray-700\">Select Image</label>\r\n                <div class=\"mt-1 flex items-center\">\r\n                    <input type=\"file\" name=\"image\" id=\"image\" accept=\"image/*\" class=\"file-input block w-full text-sm text-gray-900 border border-gray-300 rounded-lg cursor-pointer bg-gray-50 focus:outline-none focus:border-blue-500\">\r\n                </div>\r\n            </div>\r\n            <div class=\"flex items-center justify-between\">\r\n                <button type=\"submit\" class=\"w-full bg-blue-500 text-white font-bold py-2 px-4 rounded-lg hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-opacity-50 transition duration-300 ease-in-out transform hover:scale-105\">\r\n                    <i class=\"fas fa-upload mr-2\"></i> Upload Image\r\n                </button>\r\n            </div>\r\n        </form>\r\n    </div>\r\n</body>\r\n</html>";
    }

    public static string GetHtmlFormErrore()
    {
        return "<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>Image Upload Form</title>\r\n    <script src=\"https://cdn.tailwindcss.com\"></script>\r\n    <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.3/css/all.min.css\"></link>\r\n    <link href=\"https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap\" rel=\"stylesheet\">\r\n    <style>\r\n        body {\r\n            font-family: 'Roboto', sans-serif;\r\n        }\r\n        .file-input::-webkit-file-upload-button {\r\n            visibility: hidden;\r\n        }\r\n        .file-input::before {\r\n            content: 'Select Image';\r\n            display: inline-block;\r\n            background: linear-gradient(to right, #4f46e5, #3b82f6);\r\n            border: 1px solid #4f46e5;\r\n            border-radius: 0.375rem;\r\n            padding: 0.5rem 1rem;\r\n            outline: none;\r\n            white-space: nowrap;\r\n            cursor: pointer;\r\n            color: white;\r\n            font-weight: 700;\r\n            font-size: 0.875rem;\r\n            transition: background 0.3s ease;\r\n        }\r\n        .file-input:hover::before {\r\n            background: linear-gradient(to right, #3b82f6, #4f46e5);\r\n        }\r\n        .file-input:active::before {\r\n            background: linear-gradient(to right, #3b82f6, #4f46e5);\r\n        }\r\n    </style>\r\n</head>\r\n<body class=\"bg-gray-100 flex items-center justify-center min-h-screen\">\r\n    <div class=\"bg-white p-8 rounded-lg shadow-lg w-full max-w-md\">\r\n        <h2 class=\"text-2xl font-bold mb-6 text-center\">Upload Your Image</h2>\r\n        <div id=\"empty-message\" class=\"text-sm text-red-500 font-medium text-center mb-4 hidden\">Empty image</div>\r\n        <form action=\"\\\" method=\"POST\" enctype=\"multipart/form-data\" class=\"space-y-6\">\r\n            <div>\r\n                <label for=\"image\" class=\"block text-sm font-medium text-gray-700\">Select Image</label>\r\n                <div class=\"mt-1 flex items-center\">\r\n                    <input type=\"file\" name=\"image\" id=\"image\" accept=\"image/*\" class=\"file-input block w-full text-sm text-gray-900 border border-gray-300 rounded-lg cursor-pointer bg-gray-50 focus:outline-none focus:border-blue-500\">\r\n                </div>\r\n            </div>\r\n            <div class=\"flex items-center justify-between\">\r\n                <button type=\"submit\" class=\"w-full bg-blue-500 text-white font-bold py-2 px-4 rounded-lg hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-opacity-50 transition duration-300 ease-in-out transform hover:scale-105\">\r\n                    <i class=\"fas fa-upload mr-2\"></i> Upload Image\r\n                </button>\r\n            </div>\r\n        </form>\r\n    </div>\r\n    <script>\r\n        const form = document.querySelector('form');\r\n        const fileInput = document.querySelector('#image');\r\n        const emptyMessage = document.querySelector('#empty-message');\r\n\r\n        form.addEventListener('submit', (event) => {\r\n            if (!fileInput.value) {\r\n                event.preventDefault(); // Prevent form submission\r\n                emptyMessage.classList.remove('hidden');\r\n            }\r\n        });\r\n    </script>\r\n</body>\r\n</html>";
    }

    private static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}