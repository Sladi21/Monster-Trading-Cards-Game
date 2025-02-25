using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Npgsql;

namespace Monster_Trading_Cards_Game;

public class User
{
    private static readonly UserRepository _userRepo = new UserRepository();

    public static (int statusCode, string responseBody) RegisterUser(ParsedRequest request)
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

            if (_userRepo.UserExists(username))
            {
                return (409, JsonSerializer.Serialize(new { error = "User already exists" }));
            }

            var (passwordHash, salt) = SecurityService.CalculatePasswordHashAndSalt(password);
            _userRepo.InsertUser(username, passwordHash, salt);

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

    public static (int statusCode, string responseBody) LoginUser(ParsedRequest request)
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

            var credentials = _userRepo.GetUserCredentials(username);
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
            _userRepo.UpdateUserToken(username, token);

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
    
    // Buys a card package with the money of the provided user
    private static (int statusCode, string responseBody) BuyPackage(ParsedRequest request)
    {
        // TODO
        throw new NotImplementedException();
    }
    
    // Returns all cards that have been acquired by the user
    private static (int statusCode, string responseBody) GetUserCards(ParsedRequest request)
    {
        throw new NotImplementedException();
    }
    
    // Shows the users currently configured deck
    private static (int statusCode, string responseBody) GetUserDeck(ParsedRequest request)
    {
        throw new NotImplementedException();
    }

    // Retrieve stats for the requested user
    private static (int statusCode, string responseBody) GetStats(ParsedRequest request)
    {
        throw new NotImplementedException();
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}