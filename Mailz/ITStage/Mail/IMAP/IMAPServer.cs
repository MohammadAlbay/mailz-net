using System.Net.Sockets;
using System.Threading.Channels;
using ITStage.Log;

namespace ITStage.Mail.IMAP
{
    public class IMAPServer : IIMapServer
    {
        const int MAX_CAPACITY = 10;

        public DualOutputLog Logger { get; set; }
        public int Port { get; set; }
        public bool UseSSL { get; set; }
        private Channel<TcpClient> ConnectionQueue { get; set; }
        private IMAPServerConfig Config { get; set; }
        private TcpListener? listener;

        public IMAPServer(IMAPServerConfig config)
        {
            Config = config;
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
                    Logger.Log($"Handling new client: {client.Client.RemoteEndPoint}");
                    await HandleClient(client);
                }
            }
        }

        public async Task HandleClient(TcpClient client)
        {
            Logger.Log($"Started handling client: {client.Client.RemoteEndPoint}");
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    using StreamReader reader = new(stream);

                    string command = await reader.ReadLineAsync() ?? "";
                    await ParseCommands(command, client);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }


                // Handle client communication here
            }
        }

        public async Task ParseCommands(string command, TcpClient? client)
        {
            Logger.Log($"Parsing command: {command}");
        }

        public async Task Connect()
        {
            listener = new TcpListener(System.Net.IPAddress.Any, Port);
            listener.Start();

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                await ConnectionQueue.Writer.WriteAsync(client);
                Logger.Log($"Accepted new client: {client.Client.RemoteEndPoint}");
            }
        }

        public void Disconnect()
        {
            listener?.Stop();
        }
    }
}