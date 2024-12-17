using HTTP;

namespace Program
{
    class Program
    {
        public static void Main(string[] args)
        {
            HTTPServer server = new HTTPServer(4000);
            server.Start();
        }
    }
}
