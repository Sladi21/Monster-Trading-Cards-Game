using System.Text.Json;
using Npgsql;

namespace Monster_Trading_Cards_Game;

public class User
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Salt { get; set; }
    public int Coins { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Elo { get; set; }
    public string Token { get; set; }
    public List<Card> PlayerCards { get; set; }
    public List<Card> PlayerDeck { get; set; }

    public void Win()
    {
        Wins++;
        Elo += 3;
    }

    public void Loose()
    {
        Losses++;
        Elo -= 5;
    }
    
    private static readonly UserRepository UserRepo = new UserRepository();
    private static readonly SecurityService Security = new SecurityService();

    public static async Task<(int statusCode, string responseBody)> RegisterUserAsync(ParsedRequest request)
    {
        Console.WriteLine($"Request Body: {request.Body}");

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return (400, JsonSerializer.Serialize(new { error = "Request body is empty" }));
        }

        try
        {
            var userData = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);
            if (userData == null || !userData.ContainsKey("username") || !userData.ContainsKey("password"))
            {
                return (400, JsonSerializer.Serialize(new { error = "Invalid data" }));
            }

            var username = userData["username"];
            var password = userData["password"];

            if (await UserRepo.UserExistsAsync(username))
            {
                return (409, JsonSerializer.Serialize(new { error = "User already exists" }));
            }

            var (passwordHash, salt) = Security.CalculatePasswordHashAndSalt(password);
            await UserRepo.InsertUserAsync(username, passwordHash, salt);

            return (201, JsonSerializer.Serialize(new { message = "User registered successfully" }));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON Parsing Error: {ex.Message}");
            return (400, JsonSerializer.Serialize(new { error = "Invalid JSON format" }));
        }
        catch (NpgsqlException ex)
        {
            Console.WriteLine($"Database Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Database error occurred" }));
        }
    }

    public static async Task<(int statusCode, string responseBody)> LoginUserAsync(ParsedRequest request)
    {
        try
        {
            var loginData = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);
            if (loginData == null || !loginData.ContainsKey("username") || !loginData.ContainsKey("password"))
            {
                return (400, JsonSerializer.Serialize(new { error = "Invalid data" }));
            }

            var username = loginData["username"];
            var password = loginData["password"];

            var credentials = await UserRepo.GetUserCredentialsAsync(username);
            if (credentials == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Invalid username or password" }));
            }

            var (storedHash, storedSalt) = credentials.Value;
            if (!Security.VerifyPassword(password, storedHash, storedSalt))
            {
                return (401, JsonSerializer.Serialize(new { error = "Invalid username or password" }));
            }

            var token = GenerateToken();
            await UserRepo.UpdateUserTokenAsync(username, token);

            return (200, JsonSerializer.Serialize(new { message = "Login successful", token }));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON Parsing Error: {ex.Message}");
            return (400, JsonSerializer.Serialize(new { error = "Invalid JSON format" }));
        }
        catch (NpgsqlException ex)
        {
            Console.WriteLine($"Database Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Database error occurred" }));
        }
    }
    
    public static async Task<(int statusCode, string responseBody)> GetCardsAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await UserRepo.GetUserByTokenAsync(token);
            if (user == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            if (user.PlayerCards.Count < 1)
            {
                return (403, JsonSerializer.Serialize(new { error = "Player has no card." }));
            }

            return (200, JsonSerializer.Serialize(new { message = "Cards retrieved successfully", cards = user.PlayerCards }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }
    
    public static async Task <(int statusCode, string responseBody)> GetDeckAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await UserRepo.GetUserByTokenAsync(token);
            if (user == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            if (user.PlayerDeck.Count < 1)
            {   
                // TODO: Replace 200 with 204. 204 doesnt work for some reason?
                return (200, JsonSerializer.Serialize(
                        new { error = "The request was fine, but the deck doesn't have any cards" }));
            }

            return (200, JsonSerializer.Serialize
                (new { message = "The deck has cards, the response contains these", deck = user.PlayerDeck }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    public static async Task<(int statusCode, string responseBody)> GetStatsAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await UserRepo.GetUserByTokenAsync(token);
            if (user == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            return (200, JsonSerializer.Serialize(new 
            { 
                message = "The stats could be retrieved successfully.", 
                conins = user.Coins,
                wins = user.Wins,
                losses = user.Losses,
                elo = user.Elo
                
            }));
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    public static async Task <(int statusCode, string responseBody)> GetScoreboardAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }
            
            var scoreboard = await UserRepo.GetScoreboardAsync();
            
            var leaderboard = scoreboard.Select(s => new 
            {
                username = s.username,
                userCoins = s.userCoins,
                userWins = s.userWins,
                userLosses = s.userLosses,
                userElo = s.userElo
            }).ToList();
            
            return (200, JsonSerializer.Serialize(new { message = "The scoreboard could be retrieved successfully.", leaderboard }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    public static async Task<(int statusCode, string responseBody)> UpdateDeckAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await UserRepo.GetUserByTokenAsync(token);
            if (user == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            if (!root.TryGetProperty("cardIds", out var cardIdsElement) || cardIdsElement.ValueKind != JsonValueKind.Array)
            {
                return (400, JsonSerializer.Serialize(new { error = "Invalid request format." }));
            }

            var cardIds = cardIdsElement.EnumerateArray().Select(c => c.GetInt32()).ToList();

            if (cardIds.Count != 4)
            {
                return (400, JsonSerializer.Serialize(new { error = "You must select exactly 4 cards for your deck." }));
            }

            // Validate ownership (ensure all selected cards belong to the player)
            var ownedCardIds = user.PlayerCards
                .Select(c => c.Id)  // Directly access the Id property of Card objects
                .ToHashSet();

            if (!cardIds.All(id => ownedCardIds.Contains(id)))
            {
                return (403, JsonSerializer.Serialize(new { error = "You do not own some of the selected cards." }));
            }

            await UserRepo.SetUserDeckAsync(user.Username, cardIds);
            
            return (200, JsonSerializer.Serialize(new { message = "Deck updated successfully." }));
        }
        
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    public static async Task<(int statusCode, string responseBody)> LogoutAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            var logoutSuccessful = await UserRepo.LogoutUserAsync(token);

            if (!logoutSuccessful)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            
            return (200, JsonSerializer.Serialize(new { message = "Logout successful." }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}