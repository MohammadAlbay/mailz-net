using System.Net.Security;
using System.Net.Sockets;

namespace ITStage.Mail.IMAP
{
    public interface IIMapServer
    {
        Task<string> ReadLineAsync(StreamReader reader);
        Task Initialize();
        Task ParseCommands(string command, TcpClient? client, StreamWriter writer, StreamReader reader, SslStream sslStream);
        Task HandleClient(TcpClient client);
        Task RespondToClient(TcpClient client, Stream stream, string response);
        Task Connect();
        void Disconnect();
        // Other IMAP server related methods
    }
}