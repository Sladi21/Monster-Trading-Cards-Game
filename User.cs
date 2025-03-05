using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Npgsql;

namespace Monster_Trading_Cards_Game;

public class User
{
    private static readonly UserRepository _userRepo = new UserRepository();

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

            if (await _userRepo.UserExistsAsync(username))
            {
                return (409, JsonSerializer.Serialize(new { error = "User already exists" }));
            }

            var (passwordHash, salt) = SecurityService.CalculatePasswordHashAndSalt(password);
            await _userRepo.InsertUserAsync(username, passwordHash, salt);

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

            var credentials = await _userRepo.GetUserCredentialsAsync(username);
            if (credentials == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Invalid username or password" }));
            }

            var (storedHash, storedSalt) = credentials.Value;
            if (!SecurityService.VerifyPassword(password, storedHash, storedSalt))
            {
                return (401, JsonSerializer.Serialize(new { error = "Invalid username or password" }));
            }

            var token = GenerateToken();
            await _userRepo.UpdateUserTokenAsync(username, token);

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
    
    // Returns all cards that have been acquired by the user
    public static async Task<(int statusCode, string responseBody)> GetCardsAsync(ParsedRequest request)
    {
        try
        {
            // 🔹 Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // 🔹 Validate token and get user info
            var (username, userCards) = await _userRepo.GetUserCardsAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            // 🔹 Check if user has cards
            if (userCards.Count < 1)
            {
                return (403, JsonSerializer.Serialize(new { error = "Player has no card." }));
            }

            return (200, JsonSerializer.Serialize(new { message = "Cards retrieved successfully", cards = userCards }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }
    
    // Shows the users currently configured deck
    public static async Task <(int statusCode, string responseBody)> GetDeckAsync(ParsedRequest request)
    {
        try
        {
            // 🔹 Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // 🔹 Validate token and get user info
            var (username, userDeck) = await _userRepo.GetUserDeckAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            if (userDeck.Count < 1)
            {   
                // TODO: Replace 200 with 204. 204 doesnt work for some reason
                return (200, JsonSerializer.Serialize(
                        new { error = "The request was fine, but the deck doesn't have any cards" }));
            }

            return (200, JsonSerializer.Serialize
                (new { message = "The deck has cards, the response contains these", deck = userDeck }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    // Retrieve stats for the requested user
    public static async Task<(int statusCode, string responseBody)> GetStatsAsync(ParsedRequest request)
    {
        try
        {
            // 🔹 Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // 🔹 Validate token and get user info
            var (username, userCoins, userWins, userLosses, userElo) = await _userRepo.GetUserStatsAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            return (200, JsonSerializer.Serialize(new 
            { 
                message = "The stats could be retrieved successfully.", 
                conins = userCoins,
                wins = userWins,
                losses = userLosses,
                elo = userElo
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
            // 🔹 Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            // TODO: Auth
            /*
            // Validate token and get user info
            var scoreboard = _userRepo.GetScoreboard();
            if (scoreboard.Count < 1)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            */
            
            var scoreboard = await _userRepo.GetScoreboardAsync();
            
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
            // 🔹 Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // 🔹 Validate token and get user info
            var (username, userCards) = await _userRepo.GetUserCardsAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            
            // 🔹 Parse the request body (expected: { "cardIds": [1, 5, 3, 2] })
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            if (!root.TryGetProperty("cardIds", out var cardIdsElement) || cardIdsElement.ValueKind != JsonValueKind.Array)
            {
                return (400, JsonSerializer.Serialize(new { error = "Invalid request format." }));
            }

            // 🔹 Extract card IDs from JSON
            var cardIds = cardIdsElement.EnumerateArray().Select(c => c.GetInt32()).ToList();

            if (cardIds.Count != 4)
            {
                return (400, JsonSerializer.Serialize(new { error = "You must select exactly 4 cards for your deck." }));
            }

            // Validate ownership (ensure all selected cards belong to the player)
            var ownedCardIds = userCards
                .Cast<JsonElement>()  // Ensure proper JSON handling
                .Select(c => c.GetProperty("Id").GetInt32()) // Extract "Id" field as int
                .ToHashSet();

            if (!cardIds.All(id => ownedCardIds.Contains(id)))
            {
                return (403, JsonSerializer.Serialize(new { error = "You do not own some of the selected cards." }));
            }

            // 🔹 Store deck in database
            await _userRepo.SetUserDeckAsync(username, cardIds);
            
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
            // Extract token from Authorization header
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            var logoutSuccessful = await _userRepo.LogoutUserAsync(token);

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