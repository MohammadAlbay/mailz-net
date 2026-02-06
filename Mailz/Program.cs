// See https://aka.ms/new-console-template for more information
using System;
using ITStage.Mail;
using ITStage.Mail.IMAP;

Console.WriteLine("Starting Mailz Unified Mail Server...");
var config = UnifiedMailServerConfig.LoadConfig("/etc/mailz/config/ums.json");


Task.WaitAll([
    Task.Run(async () =>
{
    IMAPServer imapServer = new IMAPServer(config);
    await imapServer.Initialize();
    await imapServer.Connect();
})
]);
Console.WriteLine("Hello, World! Build Success!");
