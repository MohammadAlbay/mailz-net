using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITStage.Mail;

[JsonSerializable(typeof(UserDataRoot))]
[JsonSerializable(typeof(UserModel))]
internal partial class UserJsonContext : JsonSerializerContext
{
}

public class UserDataRoot
{
    [JsonPropertyName("users")]
    public List<UserModel> Users { get; set; } = new();
}

public class UserModel
{
    [JsonPropertyName("account")]
    public string Email { get; set; } = "";

    [JsonPropertyName("password")]
    public JsonElement RawPassword { get; set; } // Using JsonElement is safer for mixed types (int/string)

    [JsonIgnore]
    public string Password => RawPassword.ValueKind switch
    {
        JsonValueKind.String => RawPassword.GetString() ?? "",
        JsonValueKind.Number => RawPassword.GetRawText(), // Handles 1234 without quotes
        _ => ""
    };

    [JsonIgnore]
    public Dictionary<string, SessionDetail> Sessions { get; set; } = new();

    public string Username
    {
        get
        {
            if (string.IsNullOrEmpty(Email)) return "Unknown";
            var usernamePart = Email.Split('@')[0];
            return string.Join(' ', usernamePart.Split('.')
                .Select(part => char.ToUpper(part[0]) + part.Substring(1)));
        }
    }

    public Dictionary<string, SessionDetail> GetActiveSessions() => Sessions;

    public bool VerifyPassword(string password) => Password == password;

    public static UserModel? Find(string email) =>
        AllUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public static bool IsLoggedIn(UserModel user, string ip) => user.Sessions.ContainsKey(ip);

    public static void Login(UserModel user, string ip, string port = "unknown")
    {
        if (!user.Sessions.ContainsKey(ip)) user.Sessions.Add(ip, new SessionDetail { IP = ip, Port = port, LoginTime = DateTime.Now });
    }

    public static void Logout(UserModel user, string ip) => user.Sessions.Remove(ip);

    public static List<UserModel> AllUsers { get; private set; } = new();

    public static void LoadUsers(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            Console.WriteLine($"[ERROR] User file missing at: {jsonFilePath}");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(jsonFilePath);

            // CRITICAL: Use the Source Generator context to avoid Reflection errors
            var root = JsonSerializer.Deserialize(jsonString, UserJsonContext.Default.UserDataRoot);

            if (root != null)
            {
                AllUsers = root.Users;
                Console.WriteLine($"[AUTH] Successfully loaded {AllUsers.Count} users.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH] Failed to load users: {ex.Message}");
        }
    }
}