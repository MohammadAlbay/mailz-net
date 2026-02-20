using System.Net.Sockets;
using System.IO;
using System.Net.Security;
using System.Threading.Tasks;

namespace ITStage.Mail.SMTP;

public interface IMailTransfereAgent
{
    // public Task SendEmailAsync(string from, string to, string subject, string body);
    Task<string> ReadLineAsync(StreamReader reader);
    Task Initialize();
    Task ConnectAsync();
    Task LogAsync(string message);
    public Task HandleClientAsync(TcpClient client);
    public Task<bool> ParseCommandAsync(string command, TcpClient client, StreamWriter writer, StreamReader reader, SslStream sslStream);
    public Task SendResponseAsync(TcpClient client, SslStream sslStream, string response);
    public Task SendResponseAsync(TcpClient client, Stream stream, byte[] response);

}