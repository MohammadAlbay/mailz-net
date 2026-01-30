// See https://aka.ms/new-console-template for more information
using System;
using ITStage.Mail;
using ITStage.Mail.IMAP;

var config = UnifiedMailServerConfig.LoadConfig("/opt/mailz-net/config/ums.json");

_ = Task.Run(async () =>
{
    IMAPServer imapServer = new IMAPServer(config);
    await imapServer.Initialize();
    await imapServer.Connect();
});
// Console.WriteLine("Hello, World! Build Success!");
