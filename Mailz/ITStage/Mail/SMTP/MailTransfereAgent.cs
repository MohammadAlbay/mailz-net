using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using ITStage.Log;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using ITStage.Mail.IMAP;


namespace ITStage.Mail.SMTP
{
    public class MailTransfereAgent : IMailTransfereAgent
    {
        const int MAX_CAPACITY = 10;
        private readonly UnifiedMailServerConfig Config;
        private readonly DualOutputLog Logger;

        private Channel<TcpClient> ConnectionQueue { get; set; }
        private TcpListener? listener;
        private X509Certificate2? sslCertificate;

        public MailTransfereAgent(UnifiedMailServerConfig config, DualOutputLog logger)
        {
            Config = config;
            Logger = logger;
            ConnectionQueue = Channel.CreateBounded<TcpClient>(MAX_CAPACITY);
        }
        public async Task<string> ReadLineAsync(StreamReader reader)
        {
            var line = await reader.ReadLineAsync();
            return line ?? string.Empty;
        }

        private async Task LoadSecureConnectionCertificates()
        {
            if (!string.IsNullOrEmpty(Config.SSLCertificatePath) && !string.IsNullOrEmpty(Config.SSLCertificateKey))
            {
                try
                {
                    // Load the certificate and key

                    await LogAsync($"Loading SSL/TLS certificate from {Config.SSLCertificatePath}\nand key from {Config.SSLCertificateKey}");
                    var certBuffer = File.ReadAllText(Config.SSLCertificatePath);
                    var keyBuffer = File.ReadAllText(Config.SSLCertificateKey);

                    sslCertificate = X509Certificate2.CreateFromPem(certBuffer, keyBuffer);
                    // Store the certificate for later use in SSL/TLS connections
                    // For example, you could assign it to a property or use it in your connection handling logic
                    await LogAsync("SSL/TLS certificate loaded successfully.");
                }
                catch (Exception ex)
                {
                    await LogAsync($"Error loading SSL/TLS certificate: {ex.Message}");
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
                    await HandleClientAsync(client);
                }
            }
        }
        public async Task Initialize()
        {
            await LoadSecureConnectionCertificates();
            await _initWorkers();
        }
        public Task LogAsync(string message)
        {
            return Logger.LogAsync($"MTA: {message}");
        }

        public async Task HandleClientAsync(TcpClient client)
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
                    await SendResponseAsync(client, sslStream, "* OK IMAP4rev1 Service Ready\r\n");

                    while (client.Connected)
                    {
                        if (!sslStream.CanRead || !sslStream.CanWrite) break;


                        string command = await reader.ReadLineAsync() ?? "";
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            await LogAsync($"Client {client.Client.RemoteEndPoint} disconnected.");
                            break;
                        }

                        if (!await ParseCommandAsync(command, client, writer, reader, sslStream))
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

        public async Task<bool> ParseCommandAsync(string command, TcpClient client, StreamWriter writer, StreamReader reader, SslStream sslStream)
        {
            // TODO: Implement command parsing logic
            await LogAsync($"Received command: {command}");
            await SendResponseAsync(client, sslStream, $"* OK Command received {command}\r\n");
            return true;
        }

        public async Task SendResponseAsync(TcpClient client, SslStream sslStream, string response)
        {
            byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
            await sslStream.WriteAsync(responseBytes);
            await sslStream.FlushAsync();
        }
        public Task SendResponseAsync(TcpClient client, Stream stream, byte[] response)
        {
            // TODO: Implement response sending logic (byte[])
            return Task.CompletedTask;
        }



        public async Task ConnectAsync()
        {
            try
            {
                listener = new TcpListener(System.Net.IPAddress.Any, Config.SMTPInPort);
                listener.Start();
                await LogAsync($"SMTP Server started on port {Config.SMTPInPort}");

                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    await LogAsync($"New client connected: {client.Client.RemoteEndPoint}");
                    await ConnectionQueue.Writer.WriteAsync(client);
                }
            }
            catch (Exception ex)
            {
                await LogAsync($"Error in ConnectAsync: {ex.Message}");
            }
        }
    }
}