using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;


namespace ITStage.Mail
{
    [JsonSerializable(typeof(UnifiedMailServerConfig))]
    public partial class ConfigJsonContext : JsonSerializerContext { }

    public struct UnifiedMailServerConfig
    {
        [JsonPropertyName("Port.IMAP")]
        public int ImapPort { get; set; }

        [JsonPropertyName("Port.SMTP.Sub")]
        public int SMTPSubmisstionPort { get; set; }

        [JsonPropertyName("Port.SMTP.In")]
        public int SMTPInPort { get; set; }

        [JsonPropertyName("TLS_Path")]
        public string SSLCertificatePath { get; set; }

        [JsonPropertyName("TLS_Key")]
        public string SSLCertificateKey { get; set; }

        [JsonPropertyName("DKIM_Path")]
        public string DKIMPrivateKeyPath { get; set; }


        [JsonPropertyName("Storage_Path")]
        public string StoragePath { get; set; }

        [JsonPropertyName("Log_FilePath")]
        public string LogPath { get; set; }

        [JsonPropertyName("Queue_Path")]
        public string QueuePath { get; set; }

        [JsonPropertyName("Users_JsonPath")]
        public string UsersJSONPath { get; set; }

        [JsonPropertyName("BlockedDomains_FilePath")]
        public string BlockedDomainsJSONPath { get; set; }


        [JsonConstructor]
        public UnifiedMailServerConfig(
            int imapPort,
            int smtpSubPort,
            int smtpInPort,
            string sslCertificatePath,
            string sslCertificateKey,
            string dkimPrivateKeyPath,
            string storagePath,
            string logPath,
            string queuePath,
            string usersJSONPath,
            string blockedDomainsJSONPath)
        {
            ImapPort = imapPort;
            SMTPInPort = smtpInPort;
            SMTPSubmisstionPort = smtpSubPort;
            SSLCertificatePath = sslCertificatePath;
            SSLCertificateKey = sslCertificateKey;
            DKIMPrivateKeyPath = dkimPrivateKeyPath;
            StoragePath = storagePath;
            LogPath = logPath;
            QueuePath = queuePath;
            UsersJSONPath = usersJSONPath;
            BlockedDomainsJSONPath = blockedDomainsJSONPath;
        }



        public static UnifiedMailServerConfig LoadConfig(string jsonFile)
        {
            string configText = File.ReadAllText(jsonFile);
            return JsonSerializer.Deserialize(
                configText,
                ConfigJsonContext.Default.UnifiedMailServerConfig
            );
        }

        public static async Task<UnifiedMailServerConfig> LoadConfigAsync(string jsonFile)
        {
            string configText = await File.ReadAllTextAsync(jsonFile);
            return JsonSerializer.Deserialize(
                configText,
                ConfigJsonContext.Default.UnifiedMailServerConfig
            );
        }

    }
}