using System.Net.Sockets;
using System.Threading.Channels;
using ITStage.Log;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

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
        private X509Certificate2? sslCertificate;

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
            await LoadSecureConnectionCertificates();
        }

        private async Task LoadSecureConnectionCertificates()
        {
            if (!string.IsNullOrEmpty(Config.SSLCertificatePath) && !string.IsNullOrEmpty(Config.SSLCertificateKey))
            {
                try
                {
                    // Load the certificate and key

                    Logger.Log($"Loading SSL/TLS certificate from {Config.SSLCertificatePath}");
                    sslCertificate = X509CertificateLoader.LoadCertificateFromFile(Config.SSLCertificatePath);
                    // Store the certificate for later use in SSL/TLS connections
                    // For example, you could assign it to a property or use it in your connection handling logic
                    await Logger.LogAsync("SSL/TLS certificate loaded successfully.");
                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error loading SSL/TLS certificate: {ex.Message}");
                }
            }
            else
            {
                await Logger.LogAsync("SSL/TLS certificate path or key is not configured. IMAP server will run without SSL/TLS.");
            }
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
                    using var sslStream = new SslStream(stream, false);

                    await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = sslCertificate,
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                        ClientCertificateRequired = false
                    });

                    using StreamReader reader = new(sslStream);
                    using StreamWriter writer = new(sslStream) { AutoFlush = true };
                    /*only once*/
                    await RespondToClient(client, sslStream, "* OK IMAP4rev1 Service Ready\r\n");

                    while (client.Connected)
                    {
                        if (!sslStream.CanRead || !sslStream.CanWrite) break;


                        string command = await reader.ReadLineAsync() ?? "";
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            await Logger.LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        await ParseCommands(command, client, writer, reader, sslStream);
                    }

                }
                catch (Exception ex)
                {
                    await Logger.LogAsync($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }
            }
        }



        public async Task ParseCommands(string command, TcpClient? client, StreamWriter writer, StreamReader reader, SslStream sslStream)
        {
            await Logger.LogAsync($"{client.Client.RemoteEndPoint}: Parsing command: {command}");

            // Extract Tag, Command, and Arguments
            var parts = command.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await RespondToClient(client, sslStream, "* BAD Invalid command format");
                return;
            }

            var tag = parts[0];
            var cmd = parts[1].ToUpper();
            var args = parts.Length > 2 ? parts[2] : "";

            switch (cmd)
            {
                case "CAPABILITY":
                    await RespondToClient(client, sslStream, "* CAPABILITY IMAP4rev1 AUTH=PLAIN LOGIN");
                    await RespondToClient(client, sslStream, $"{tag} OK CAPABILITY completed");
                    break;
                case "AUTHENTICATE":
                    await RespondToClient(client, sslStream, "+ ");
                    // READ user & pass as base64 encoded string
                    string authData = await ReadLineAsync(reader);
                    Logger.Log($"Received AUTH data from {client.Client.RemoteEndPoint}: {authData}");
                    byte[] binaryAuth = Convert.FromBase64String(authData.Trim());
                    string decodedAuth = System.Text.Encoding.UTF8.GetString(binaryAuth);
                    string[] authParts = decodedAuth.Split('\0');
                    Logger.Log($"Decoded AUTH data from {client.Client.RemoteEndPoint}: {decodedAuth}");

                    if (authParts.Length == 3)
                    {
                        string authType = authParts[0];
                        string username = authParts[1];
                        string password = authParts[2];
                        var result = await Authenticate(authType, username, password, client, writer);
                        if (!result)
                        {
                            await RespondToClient(client, sslStream, $"{tag} NO Authentication failed");
                            return;
                        }
                        else
                        {
                            await RespondToClient(client, sslStream, $"{tag} OK Authentication successful");
                        }
                    }
                    else
                    {
                        await RespondToClient(client, sslStream, $"{tag} NO Invalid authentication data");
                    }
                    // await RespondToClient(client, writer.BaseStream, $"Command is '{command}'");
                    await RespondToClient(client, sslStream, $"{tag} OK LOGIN completed");
                    break;
                case "HELLO":
                case "OLHA":
                    await RespondToClient(client, sslStream, "Hello! Welcome to the IMAP server.");
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