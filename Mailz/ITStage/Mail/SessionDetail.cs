namespace ITStage.Mail;

public class SessionDetail
{
    public string Port { get; set; } = "";
    public string IP { get; set; } = "";
    public DateTime LoginTime { get; set; } = DateTime.Now;

    public override string ToString() => $"{IP}:{Port} (logged in at {LoginTime:yyyy-MM-dd HH:mm:ss})";
}