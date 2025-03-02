using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Monster_Trading_Cards_Game;

public class UserRepository
{
    private readonly string _connectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";
    
    public bool UserExists(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        return (long)cmd.ExecuteScalar() > 0;
    }

    public void InsertUser(string username, string passwordHash, string salt)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (username, password_hash, salt, coins, wins, losses, elo, token)
            VALUES (@username, @passwordhash, @salt, @coins, @wins, @losses, @elo, @token)", conn);

        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("passwordhash", passwordHash);
        cmd.Parameters.AddWithValue("salt", salt);
        cmd.Parameters.AddWithValue("coins", 20);
        cmd.Parameters.AddWithValue("wins", 0);
        cmd.Parameters.AddWithValue("losses", 0);
        cmd.Parameters.AddWithValue("elo", 100);
        cmd.Parameters.AddWithValue("token", ""); 

        cmd.ExecuteNonQuery();
    }

    public (string passwordHash, string salt)? GetUserCredentials(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT password_hash, salt FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetString(1));
        }

        return null;
    }

    public void UpdateUserToken(string username, string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("UPDATE users SET token = @token WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("token", token);

        cmd.ExecuteNonQuery();
    }

    public (string? username, List<object> playerCards) GetUserCards(string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        using var cmd = new NpgsqlCommand("SELECT username, player_cards FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var username = reader.GetString(0);
            var cardsJson = reader.GetValue(1); // Read JSONB as object

            // Deserialize JSONB column to List<object>
            var playerCards = cardsJson is DBNull ? new List<object>() : JsonSerializer.Deserialize<List<object>>(cardsJson.ToString()) ?? new List<object>();

            return (username, playerCards);
        }

        return (null, new List<object>());
    }

    public (string? username, List<object> playerDeck) GetUserDeck(string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        using var cmd = new NpgsqlCommand("SELECT username, player_deck FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var username = reader.GetString(0);
            var cardsJson = reader.GetValue(1); // Read JSONB as object

            // Deserialize JSONB column to List<object>
            var playerDeck = cardsJson is DBNull ? new List<object>() : JsonSerializer.Deserialize<List<object>>(cardsJson.ToString()) ?? new List<object>();

            return (username, playerDeck);
        }

        return (null, new List<object>());
    }

    public (string? username, int userCoins, int userWins, int userLosses, int userElo) GetUserStats(string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        using var cmd = new NpgsqlCommand("SELECT username, coins, wins, losses, elo FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var username = reader.GetString(0);
            var coins = reader.GetInt32(1);
            var wins = reader.GetInt32(2);
            var losses = reader.GetInt32(3);
            var elo = reader.GetInt32(4);
            
            return (username, coins, wins, losses, elo);
        }
        
        return (null, 0,0, 0,0);
    }
    
    // Retrieves the user scoreboard ordered by the user's ELO.
    public List<(string username, int userCoins, int userWins, int userLosses, int userElo)> GetScoreboard()
    {
        var userStatsList = new List<(string, int, int, int, int)>();
        
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        using var cmd = new NpgsqlCommand("SELECT username, coins, wins, losses, elo FROM users ORDER BY elo DESC", conn);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var username = reader.GetString(0);
            var coins = reader.GetInt32(1);
            var wins = reader.GetInt32(2);
            var losses = reader.GetInt32(3);
            var elo = reader.GetInt32(4);

            userStatsList.Add((username, coins, wins, losses, elo));
        }

        return userStatsList;
    }
    
    public void SetUserDeck(string username, List<int> cardIds)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // 🔹 Fetch all player's cards (stored as JSONB)
        using var fetchCmd = new NpgsqlCommand("SELECT player_cards FROM users WHERE username = @username", conn);
        fetchCmd.Parameters.AddWithValue("username", username);

        string playerCardsJson = fetchCmd.ExecuteScalar()?.ToString() ?? "[]"; // Ensure it's never null
        var playerCards = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(playerCardsJson) ?? new List<Dictionary<string, object>>();

        // 🔹 Filter the full card objects based on the selected IDs
        var selectedCards = playerCards
            .Where(card => card.ContainsKey("Id") && card["Id"] is JsonElement jsonId && cardIds.Contains(jsonId.GetInt32()))
            .ToList();

        // 🔹 Save the full card objects in player_deck
        using var cmd = new NpgsqlCommand("UPDATE users SET player_deck = @deck WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
    
        string jsonDeck = JsonSerializer.Serialize(selectedCards);
        cmd.Parameters.Add("deck", NpgsqlDbType.Jsonb).Value = jsonDeck;

        cmd.ExecuteNonQuery();
    }

    public bool LogoutUser(string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // Check if the token exists
        using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE token = @token", conn);
        checkCmd.Parameters.AddWithValue("token", token);

        var count = (long)checkCmd.ExecuteScalar(); // ExecuteScalar returns object, cast to long

        if (count == 0)
        {
            return false; // Token not found, logout failed
        }

        // Invalidate the token by replacing it with a new random value
        using var updateCmd = new NpgsqlCommand("UPDATE users SET token = @newToken WHERE token = @oldToken", conn);
        updateCmd.Parameters.AddWithValue("newToken", Guid.NewGuid().ToString());
        updateCmd.Parameters.AddWithValue("oldToken", token);

        updateCmd.ExecuteNonQuery();
        return true; // Logout successful
    }


}