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
                    using StreamWriter writer = new(stream) { AutoFlush = true };
                    for (; ; )
                    {
                        if (!stream.CanRead) break;

                        string command = await reader.ReadLineAsync() ?? "";
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            await Logger.LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        await ParseCommands(command, client, writer);
                    }

                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }


                // Handle client communication here
            }
        }

        public Task RespondToClient(TcpClient client, Stream stream, string response)
        {
            return Task.Run(async () =>
            {
                try
                {

                    await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(response + "\r\n"));
                    await Logger.LogAsync($"Sent response to {client.Client.RemoteEndPoint}: {response}");
                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error responding to client {client.Client.RemoteEndPoint}: {ex.Message}");
                }
            });
        }

        public async Task ParseCommands(string command, TcpClient? client, StreamWriter? writer = null)
        {
            await Logger.LogAsync($"{client.Client.RemoteEndPoint}: Parsing command: {command}");
            switch (command.ToUpper())
            {
                case "CAPABILITY":
                    await RespondToClient(client, writer.BaseStream, "IMAP4rev1 STARTTLS LOGINDISABLED");
                    break;
                case "Hello":
                case "OLHA":
                    await RespondToClient(client, writer.BaseStream, "Hello! Welcome to the IMAP server.");
                    break;
            }
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