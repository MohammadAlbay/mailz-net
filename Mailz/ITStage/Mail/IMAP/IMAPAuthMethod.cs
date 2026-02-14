using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Channels;
using ITStage.Log;


namespace ITStage.Mail.IMAP
{
    public partial class IMAPServer
    {
        public async Task<bool> Authenticate(string mechanism, string user, string password, TcpClient client, StreamWriter writer)
        {
            // For simplicity, we will just accept any credentials for now
            // In a real implementation, you would validate the credentials against your user database
            var userModel = UserModel.Find(user);
            if (userModel == null)
            {
                await RespondToClient(client, writer.BaseStream, $"{mechanism} authentication failed: user not found");
                return false;
            }

            if (!userModel.VerifyPassword(password))
            {
                await RespondToClient(client, writer.BaseStream, $"{mechanism} authentication failed: invalid password");
                return false;
            }

            var temp = client.Client.RemoteEndPoint?.ToString()?.Split(':');
            string ip = temp != null && temp.Length > 0 ? temp[0] : "unknown";
            string port = temp != null && temp.Length > 1 ? temp[1] : "unknown";

            UserModel.Login(userModel, ip ?? "unknown", port ?? "unknown");
            // UserModel
            await RespondToClient(client, writer.BaseStream, $"{mechanism} authentication successful");
            foreach (var session in userModel.GetActiveSessions())
            {
                await Logger.LogAsync($"User {user} has active session from {session}");
            }
            return true;
        }
    }
}