using HTTP.Claster;
using System.Net.Sockets;

namespace HTTP.UserManager
{
    public class UserManager
    {
        private List<TcpClient> _users = new List<TcpClient>();
        private int _maxCountOfUsers = 100;
        private int _curCount => _users.Count;
        public int CurCount => _curCount;

        public UserManager(int maxCountUsers)
        {
            _maxCountOfUsers = maxCountUsers;
        }

        public bool QweeIsFull()
        {
            if (_curCount > _maxCountOfUsers)
            {
                return false;
            }
            else
            {
                return true;    
            }
        }

        public void AddUser(TcpClient user)
        {
            lock (_users)
            {
                _users.Add(user);
            }
        }

        public void RemoveUser(TcpClient user)
        {
            lock (_users)
            {
                _users.Remove(user);
                user.Close();
            }
        }

        public Task<byte[]> SendData(ClasterManager clasterManager, TcpClient tcpClient, byte[] bytes, int maxCount)
        {
            while (true)
            {
                lock (this)
                {
                    if (!clasterManager.FreeClasters)
                    {
                        if (maxCount > _curCount)
                        {
                            lock (this)
                            {
                                Monitor.Wait(this);
                            }
                        }
                        else
                        {
                            lock (_users)
                            {
                                _users.Remove(tcpClient);
                            }
                            Console.WriteLine(_curCount);
                            Console.WriteLine("Query is full\n");
                            throw new Exception("Query is full.");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Task<byte[]> buffer = null;
            lock (this)
            {
                buffer = clasterManager.Send(bytes);
                Monitor.Pulse(this);
            }

            lock (_users)
            {
                _users.Remove(tcpClient);
                Console.WriteLine("\n Count of users " + _curCount +"\n");
            }

            return buffer;
        }
    }
}
