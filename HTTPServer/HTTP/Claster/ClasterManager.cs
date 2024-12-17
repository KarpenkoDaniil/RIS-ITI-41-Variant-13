using System.Net;
using System.Net.Sockets;

namespace HTTP.Claster
{
    public class ClasterManager
    {
        int _numOfUsers = 0;
        int _maxUsers = 2;
        object _lock = new object();
        List<Claster> _clasters = new List<Claster>();
        Socket _clasterListener;

        public bool FreeClasters
        {
            get
            {
                return _clasters.Any(c => c.IsFree);
            }
        }

        public ClasterManager(string ipAdress, string port)
        {
            //Серверный сокет
            _clasterListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint IP = new IPEndPoint(IPEndPoint.Parse(ipAdress).Address, int.Parse(port));
            _clasterListener.Bind(IP);
            Thread thread = new Thread(ClasterCycle);
            thread.Start();
        }

        public async Task<byte[]> Send(byte[] data)
        {
            foreach (var item in _clasters)
            {
                if (item.IsFree)
                {
                    return await item.SendData(data);
                }
            }

            return null;
        }

        private void ClasterCycle()
        {
            while (true)
            {
                _clasterListener.Listen(1000);
                Socket socket = _clasterListener.Accept();
                Claster claster = new Claster(socket);
                _clasters.Add(claster);
                Console.WriteLine("Cluster " + socket.RemoteEndPoint + " connect to server");
            }
        }
    }
}
