using System.Net.Sockets;
using System.Threading.Channels;

namespace ITStage.Mail.IMAP
{
    public interface IIMapServer
    {
        Task Initialize();
        Task ParseCommands(string command, TcpClient? client);
        Task HandleClient(TcpClient client);
        Task Connect();
        void Disconnect();
        // Other IMAP server related methods
    }
}