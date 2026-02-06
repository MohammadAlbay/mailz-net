using System.Net.Sockets;
using System.Threading.Channels;
using ITStage.Log;

namespace ITStage.Mail.IMAP
{
    public class IMAPServer : IIMapServer
    {
        const int MAX_CAPACITY = 10;

        public DualOutputLog Logger { get; set; }
        public int Port { get; set; } = 993;
        public bool UseSSL { get; set; }
        private Channel<TcpClient> ConnectionQueue { get; set; }
        private UnifiedMailServerConfig Config { get; set; }
        private TcpListener? listener;

        public IMAPServer(UnifiedMailServerConfig config)
        {
            Config = config;
            Port = config.ImapPort;
            ConnectionQueue = Channel.CreateBounded<TcpClient>(MAX_CAPACITY);
            Logger = new DualOutputLog("IMAP", config.LogPath, Console.Out);
        }
        public async Task Initialize()
        {
            await _initWorkers();
        }

        private async Task _initWorkers()
        {
            for (int i = 0; i < MAX_CAPACITY; i++)
            {
                await foreach (TcpClient client in ConnectionQueue.Reader.ReadAllAsync())
                {
                    // Handle client connection
                    await Logger.LogAsync($"Handling new client: {client.Client.RemoteEndPoint}");
                    await HandleClient(client);
                }
            }
        }

        public async Task HandleClient(TcpClient client)
        {
            await Logger.LogAsync($"Started handling client: {client.Client.RemoteEndPoint}");
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    using StreamReader reader = new(stream);

                    for (; ; )
                    {
                        if (!stream.CanRead) break;

                        string command = await reader.ReadLineAsync() ?? "";
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            await Logger.LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        await ParseCommands(command, client);
                    }

                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }


                // Handle client communication here
            }
        }

        public async Task ParseCommands(string command, TcpClient? client)
        {
            await Logger.LogAsync($"{client.Client.RemoteEndPoint}: Parsing command: {command}");
        }

        public async Task Connect()
        {
            listener = new TcpListener(System.Net.IPAddress.Any, Port);
            listener.Start();
            await Logger.LogAsync($"IMAP Server started on port {Port}. Waiting for connections...");
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                await ConnectionQueue.Writer.WriteAsync(client);
                await Logger.LogAsync($"Accepted new client: {client.Client.RemoteEndPoint}");
            }
        }

        public void Disconnect()
        {
            listener?.Stop();
        }
    }
}