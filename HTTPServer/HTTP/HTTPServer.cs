using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HTTP
{
    public class HTTPServer
    {
        private TcpListener listener;
        private bool isRunning = false;

        private Claster.ClasterManager manager;
        private UserManager.UserManager userManager;

        public HTTPServer(int port)
        {
            string pathToConfig = AppDomain.CurrentDomain.BaseDirectory + "ConfigServer.json";
            string json = File.ReadAllText(pathToConfig);
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);


            listener = new TcpListener(IPAddress.Parse(dictionary["IPADRESS"]), int.Parse(dictionary["PORT"]));
            listener.Server.NoDelay = true;
            manager = new Claster.ClasterManager(dictionary["IPADRESS_CLUSTER"], dictionary["PORT_CLUSTER"]);
            userManager = new UserManager.UserManager(100);
        }

        public void Start()
        {
            isRunning = true;
            listener.Start();
            listener.Server.SendBufferSize = 1024*1024*8; // Размер буфера для отправки (64 КБ)
            listener.Server.ReceiveBufferSize = 1024*1024*8; // Размер буфера для получения (64 КБ)
            Console.WriteLine($"Server started on port {((IPEndPoint)listener.LocalEndpoint).Port}");

            while (isRunning)
            {
                TcpClient client = listener.AcceptTcpClient();
                client.SendBufferSize = 65536*8;
                client.ReceiveBufferSize = 65536*8;
                client.NoDelay = true;
                try
                {
                    if (userManager.QweeIsFull())
                    {
                        userManager.AddUser(client);
                        Thread thread = new Thread(() => HandleClient(client));
                        thread.Start();
                    }
                    else
                    {
                        throw new Exception("Query is full.");
                    }
                }
                catch (Exception ex)
                {
                    HTTPResponse response = new HTTPResponse();
                    response.SendError(client.GetStream(), ex.Message);
                    userManager.RemoveUser(client);
                    client.Close();
                }
            }
        }

        private async void HandleClient(TcpClient client)
        {
            if (client.Connected)
            {
                Console.WriteLine(client.Client.RemoteEndPoint);
                using (NetworkStream stream = client.GetStream())
                {
                    // Чтение и обработка запроса
                    HTTPRequest request = HTTPRequest.Parse(stream);
                    HTTPResponse response = new HTTPResponse();

                    if (request.Method == "GET")
                    {
                        if (request.Path == "/")
                        {
                            response.SendHtml(stream, FileHandler.GetHtmlForm());
                            userManager.RemoveUser(client);
                            client.Close();
                        }
                        else if (request.Path.StartsWith("/uploads/"))
                        {
                            FileHandler.SendFile(stream, request.Path);
                            userManager.RemoveUser(client);
                            client.Close();
                        }
                        else
                        {
                            response.SendError(stream, "404 Not Found");
                            userManager.RemoveUser(client);
                            client.Close();
                        }
                    }
                    else if (request.Method == "POST" && request.Path == "/")
                    {
                        byte[] buffer = null;
                        try
                        {
                            buffer = await FileHandler.HandleUpload(request);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == "Empty image.")
                            {
                                buffer = null;
                                userManager.RemoveUser(client);
                                response.SendHtml(stream, FileHandler.GetHtmlFormErrore());
                                client.Close();
                            }
                        }

                        if (buffer != null && buffer.Length != 0)
                        {
                            try
                            {
                                byte[] buffer_1 = userManager.SendData(manager, client, buffer, 100).Result;
                                if (buffer_1 != null)
                                {
                                    response.SendFile(stream, buffer_1, FileHandler.GetHtmlForm());
                                    userManager.RemoveUser(client);
                                    client.Close();
                                }
                                else
                                {
                                    response.SendError(stream, "Image Empty");
                                    userManager.RemoveUser(client);
                                    client.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                response.SendError(stream, ex.Message);
                                userManager.RemoveUser(client);
                                client.Close();
                            }
                        }
                        else
                        {
                            response.SendError(stream, "Image be broken");
                            userManager.RemoveUser(client);
                            client.Close();
                        }
                    }

                    userManager.RemoveUser(client);

                    Console.WriteLine("Curent count " + userManager.CurCount);
                }
            }
            else
            {
                userManager.RemoveUser(client); 
            }
        }
    }
}