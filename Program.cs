using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Monster_Trading_Cards_Game;

internal static class Program
{
    static void Main(string[] args)
    {
        Server.RunServer();
    }
}