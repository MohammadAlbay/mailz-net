// See https://aka.ms/new-console-template for more information
using System;
using ITStage.Mail;
using ITStage.Mail.IMAP;
using ITStage.Mail.SMTP;
using ITStage.Log;
Console.WriteLine("Starting Mailz Unified Mail Server...");
var config = UnifiedMailServerConfig.LoadConfig("/etc/mailz/config/ums.json");
DualOutputLog logger = new("UMS", config.LogPath, Console.Out);
IMAPServer imapServer = new(config, logger);
MailTransfereAgent mtaServer = new(config, logger);

Console.WriteLine("Init Unified Main Server");
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
    }),

    Task.Run(async() => Console.WriteLine("Unified Mail Server is running..."))
]);

