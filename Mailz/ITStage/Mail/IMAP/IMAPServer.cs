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

        public IMAPServer(UnifiedMailServerConfig config, DualOutputLog logger)
        {
            Config = config;
            Logger = logger;
            Port = config.ImapPort;
            ConnectionQueue = Channel.CreateBounded<TcpClient>(MAX_CAPACITY);
        }
        public async Task Initialize()
        {
            await LoadSecureConnectionCertificates();
            await LoadAccounts();
            await _initWorkers();

        }

        private async Task LoadAccounts()
        {
            _ = Task.Run(() => UserModel.LoadUsers(Config.UsersJSONPath));
            await Logger.LogAsync($"Loaded user accounts from {Config.UsersJSONPath}");
        }

        private async Task LoadSecureConnectionCertificates()
        {
            if (!string.IsNullOrEmpty(Config.SSLCertificatePath) && !string.IsNullOrEmpty(Config.SSLCertificateKey))
            {
                try
                {
                    // Load the certificate and key

                    Logger.Log($"Loading SSL/TLS certificate from {Config.SSLCertificatePath}\nand key from {Config.SSLCertificateKey}");
                    var certBuffer = File.ReadAllText(Config.SSLCertificatePath);
                    var keyBuffer = File.ReadAllText(Config.SSLCertificateKey);

                    sslCertificate = X509Certificate2.CreateFromPem(certBuffer, keyBuffer);
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
                    await LogAsync($"Handling new client: {client.Client.RemoteEndPoint}");
                    await HandleClient(client);
                }
            }
        }


        public async Task LogAsync(string message) => await Logger.LogAsync($"[IMAP]:{message}");


        public async Task HandleClient(TcpClient client)
        {
            await LogAsync($"Started handling client: {client.Client.RemoteEndPoint}");
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
                            await LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        if (!await ParseCommands(command, client, writer, reader, sslStream))
                        {
                            break;
                        }
                    }

                }
                catch (Exception ex)
                {
                    await LogAsync($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                }
            }
        }



        public async Task<bool> ParseCommands(string command, TcpClient? client, StreamWriter writer, StreamReader reader, SslStream sslStream)
        {
            await LogAsync($"{client.Client.RemoteEndPoint}: Parsing command: {command}");

            // Extract Tag, Command, and Arguments
            var parts = command.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await RespondToClient(client, sslStream, "* BAD Invalid command format");
                return false;
            }

            var tag = parts[0];
            var cmd = parts[1].ToUpper();
            var args = parts.Length > 2 ? parts[2] : "";
            var continueLooping = true;

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

                    await LogAsync($"Received AUTH data from {client.Client.RemoteEndPoint}: {authData}");

                    byte[] binaryAuth = Convert.FromBase64String(authData.Trim());
                    string decodedAuth = System.Text.Encoding.UTF8.GetString(binaryAuth);
                    string[] authParts = decodedAuth.Split('\0');

                    string username = "";
                    string password = "";

                    if (authParts.Length >= 3)
                    {
                        // Standard format: [AuthorizeID]\0Username\0Password
                        username = authParts[1];
                        password = authParts[2];
                    }
                    else if (authParts.Length == 2)
                    {
                        // Some non-standard clients might send: Username\0Password
                        username = authParts[0];
                        password = authParts[1];
                    }
                    else
                    {
                        await RespondToClient(client, sslStream, $"{tag} NO Invalid authentication data");
                        break;
                    }

                    await LogAsync($"Decoded AUTH credentials from {client.Client.RemoteEndPoint}: username='{username}', password='{password}'");
                    _ = await Authenticate("AUTHENTICATE", username, password, client, writer);
                    break;
                case "LOGIN":
                    var loginParts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (loginParts.Length != 2)
                    {
                        await RespondToClient(client, sslStream, $"{tag} NO Invalid LOGIN format");
                        break;
                    }
                    string loginUsername = loginParts[0];
                    string loginPassword = loginParts[1];
                    await LogAsync($"Received LOGIN command from {client.Client.RemoteEndPoint}: username='{loginUsername}'");
                    _ = await Authenticate("LOGIN", loginUsername, loginPassword, client, writer);
                    break;
                case "BYE":
                    await RespondToClient(client, sslStream, $"{tag} OK Goodbye!");
                    client.Close();
                    continueLooping = false;
                    break;
                case "HELLO":
                case "OLHA":
                    await RespondToClient(client, sslStream, $"{tag} Hello Dear! Welcome to the IMAP server.");
                    break;
                default:
                    await RespondToClient(client, sslStream, $"{tag} BAD Unknown command");
                    break;
            }

            return continueLooping;
        }

        public Task RespondToClient(TcpClient client, Stream stream, string response)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(response + "\r\n"));
                    await LogAsync($"Sent response to {client.Client.RemoteEndPoint}: {response}");
                }
                catch (Exception ex)
                {
                    await LogAsync($"Error responding to client {client.Client.RemoteEndPoint}: {ex.Message}");
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
            await LogAsync($"IMAP Server started on port {Port}. Waiting for connections...");
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                await ConnectionQueue.Writer.WriteAsync(client);
                await LogAsync($"Accepted new client: {client.Client.RemoteEndPoint}");
            }
        }

        public void Disconnect()
        {
            listener?.Stop();
        }
    }
}