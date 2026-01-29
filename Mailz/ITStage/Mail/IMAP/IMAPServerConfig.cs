namespace ITStage.Mail.IMAP
{
    public struct IMAPServerConfig
    {
        public int Port { get; set; }
        // public bool UseSSL { get; set; }
        public string SSLCertificatePath { get; set; }
        public string DKIMPrivateKeyPath { get; set; }
        public string StoragePath { get; set; }
        public string LogPath { get; set; }
        public string QueuePath { get; set; }
        public string UsersJSONPath { get; set; }
        public string BlockedDomainsJSONPath { get; set; }

        public IMAPServerConfig(int port, string sslCertificatePath = "",
            string dkimPrivateKeyPath = "", string storagePath = "",
            string logPath = "", string queuePath = "",
            string usersJSONPath = "", string blockedDomainsJSONPath = "")
        {
            Port = port;
            SSLCertificatePath = sslCertificatePath;
            DKIMPrivateKeyPath = dkimPrivateKeyPath;
            StoragePath = storagePath;
            LogPath = logPath;
            QueuePath = queuePath;
            UsersJSONPath = usersJSONPath;
            BlockedDomainsJSONPath = blockedDomainsJSONPath;
            StoragePath = storagePath;
            LogPath = logPath;
            QueuePath = queuePath;
            UsersJSONPath = usersJSONPath;
            BlockedDomainsJSONPath = blockedDomainsJSONPath;
        }
    }
}