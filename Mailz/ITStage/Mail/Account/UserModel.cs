using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITStage.Mail;


public class UserDataRoot
{
    [JsonPropertyName("users")]
    public List<UserModel> Users { get; set; } = new();
}

public class UserModel
{
    [JsonPropertyName("account")]
    public string Email { get; set; } = "";

    // Using "object" or a custom converter handles the case where JSON has 1234 instead of "1234"
    [JsonPropertyName("password")]
    public object RawPassword { get; set; } = "";

    [JsonIgnore]
    public string Password => RawPassword?.ToString() ?? "";

    public List<string> Sessions { get; set; } = new();

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

    public bool VerifyPassword(string password)
    {
        // In a real implementation, you would hash the password and compare it to a stored hash
        return Password == password;
    }
    public static UserModel? Find(string email)
    {
        return AllUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsLoggedIn(UserModel user, string ip)
    {
        return user.Sessions.Contains(ip);
    }
    public static void Login(UserModel user, string ip)
    {
        if (!user.Sessions.Contains(ip))
        {
            user.Sessions.Add(ip);
        }
    }
    public static void Logout(UserModel user, string ip)
    {
        user.Sessions.Remove(ip);
    }
    // Global list to hold the users once loaded
    public static List<UserModel> AllUsers { get; private set; } = new();

    public static void LoadUsers(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            Console.WriteLine("User file missing!");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(jsonFilePath);
            var root = JsonSerializer.Deserialize<UserDataRoot>(jsonString);

            if (root != null)
            {
                AllUsers = root.Users;
                Console.WriteLine($"Successfully loaded {AllUsers.Count} users.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load users: {ex.Message}");
        }
    }
}