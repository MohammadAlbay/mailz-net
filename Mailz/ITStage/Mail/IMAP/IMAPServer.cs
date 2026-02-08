using System.Net.Sockets;
using System.Threading.Channels;
using ITStage.Log;

namespace ITStage.Mail.IMAP
{
    public partial class IMAPServer : IIMapServer
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
                    /*only once*/
                    await RespondToClient(client, stream, "* OK IMAP4rev1 Service Ready");

                    for (; ; )
                    {
                        if (!stream.CanRead || !stream.CanWrite) break;



                        string command = await reader.ReadLineAsync() ?? "";
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            await Logger.LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        await ParseCommands(command, client, writer, reader);
                    }

                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }
            }
        }



        public async Task ParseCommands(string command, TcpClient? client, StreamWriter writer, StreamReader reader)
        {
            await Logger.LogAsync($"{client.Client.RemoteEndPoint}: Parsing command: {command}");

            // Extract Tag, Command, and Arguments
            var parts = command.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await RespondToClient(client, writer.BaseStream, "* BAD Invalid command format");
                return;
            }

            var tag = parts[0];
            var cmd = parts[1].ToUpper();
            var args = parts.Length > 2 ? parts[2] : "";

            switch (cmd)
            {
                case "CAPABILITY":
                    await RespondToClient(client, writer.BaseStream, "* CAPABILITY IMAP4rev1 AUTH=PLAIN LOGIN");
                    await RespondToClient(client, writer.BaseStream, $"{tag} OK CAPABILITY completed");
                    break;
                case "AUTHENTICATE":
                    await RespondToClient(client, writer.BaseStream, "+ ");
                    // READ user & pass as base64 encoded string
                    string authData = await ReadLineAsync(reader);
                    Logger.Log($"Received AUTH data from {client.Client.RemoteEndPoint}: {authData}");
                    var decodedAuth = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authData.Trim()));
                    Logger.Log($"Decoded AUTH data from {client.Client.RemoteEndPoint}: {decodedAuth}");
                    var authParts = decodedAuth.Split('\0');
                    if (authParts.Length == 3)
                    {
                        string authType = authParts[0];
                        string username = authParts[1];
                        string password = authParts[2];
                        var result = await Authenticate(authType, username, password, client, writer);
                        if (!result)
                        {
                            await RespondToClient(client, writer.BaseStream, $"{tag} NO Authentication failed");
                            return;
                        }
                        else
                        {
                            await RespondToClient(client, writer.BaseStream, $"{tag} OK Authentication successful");
                        }
                    }
                    else
                    {
                        await RespondToClient(client, writer.BaseStream, $"{tag} NO Invalid authentication data");
                    }
                    // await RespondToClient(client, writer.BaseStream, $"Command is '{command}'");
                    await RespondToClient(client, writer.BaseStream, $"{tag} OK LOGIN completed");
                    break;
                case "HELLO":
                case "OLHA":
                    await RespondToClient(client, writer.BaseStream, "Hello! Welcome to the IMAP server.");
                    break;
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

        public async Task<string> ReadLineAsync(StreamReader reader)
        {
            string? line = await reader.ReadLineAsync();
            return line ?? string.Empty;
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