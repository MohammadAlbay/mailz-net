// See https://aka.ms/new-console-template for more information
using System;
using ITStage.Mail;
using ITStage.Mail.IMAP;
using ITStage.Mail.SMTP;
using ITStage.Log;
Console.WriteLine("Starting Mailz Unified Mail Server...");
var config = UnifiedMailServerConfig.LoadConfig("/etc/mailz/config/ums.json");
var logger = new DualOutputLog("UMS", config.LogPath, Console.Out);
IMAPServer imapServer = new IMAPServer(config, logger);
MailTransfereAgent mtaServer = new MailTransfereAgent(config, logger);
Task.WaitAll([
    Task.Run(async () =>
    {
        await imapServer.Initialize();
    }),

    Task.Run(async () => {
        await imapServer.Connect();
    }),
    Task.Run(async () => {
        await mtaServer.Initialize();
    }),
    Task.Run(async () => {
        await mtaServer.ConnectAsync();
    })
]);
Console.WriteLine("Unified Main Server is running...");
