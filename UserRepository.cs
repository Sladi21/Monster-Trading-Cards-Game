using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Monster_Trading_Cards_Game;

public class UserRepository
{
    private readonly string _connectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";
    
    public async Task<bool> UserExistsAsync(string username)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        return (long)(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task InsertUserAsync(string username, string passwordHash, string salt)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
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

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(string passwordHash, string salt)?> GetUserCredentialsAsync(string username)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT password_hash, salt FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetString(0), reader.GetString(1));
        }

        return null;
    }

    public async Task UpdateUserTokenAsync(string username, string token)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("UPDATE users SET token = @token WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("token", token);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(string? username, List<object> playerCards)> GetUserCardsAsync(string token)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        await using var cmd = new NpgsqlCommand("SELECT username, player_cards FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
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

    public async Task <(string? username, List<object> playerDeck)> GetUserDeckAsync(string token)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        await using var cmd = new NpgsqlCommand("SELECT username, player_deck FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);
        
        await using var reader = await cmd.ExecuteReaderAsync();
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

    public async Task<(string? username, int userCoins, int userWins, int userLosses, int userElo)> GetUserStatsAsync(string token)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        await using var cmd = new NpgsqlCommand("SELECT username, coins, wins, losses, elo FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);
        
        await using var reader = await cmd.ExecuteReaderAsync();
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
    public async Task<List<(string username, int userCoins, int userWins, int userLosses, int userElo)>> GetScoreboardAsync()
    {
        var userStatsList = new List<(string, int, int, int, int)>();
        
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        await using var cmd = new NpgsqlCommand("SELECT username, coins, wins, losses, elo FROM users ORDER BY elo DESC", conn);
        
        await using var reader = await cmd.ExecuteReaderAsync();
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
    
    public async Task SetUserDeckAsync(string username, List<int> cardIds)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // 🔹 Fetch all player's cards (stored as JSONB)
        await using var fetchCmd = new NpgsqlCommand("SELECT player_cards FROM users WHERE username = @username", conn);
        fetchCmd.Parameters.AddWithValue("username", username);

        string playerCardsJson = fetchCmd.ExecuteScalar()?.ToString() ?? "[]"; // Ensure it's never null
        var playerCards = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(playerCardsJson) ?? new List<Dictionary<string, object>>();

        // 🔹 Filter the full card objects based on the selected IDs
        var selectedCards = playerCards
            .Where(card => card.ContainsKey("Id") && card["Id"] is JsonElement jsonId && cardIds.Contains(jsonId.GetInt32()))
            .ToList();

        // 🔹 Save the full card objects in player_deck
        await using var cmd = new NpgsqlCommand("UPDATE users SET player_deck = @deck WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
    
        string jsonDeck = JsonSerializer.Serialize(selectedCards);
        cmd.Parameters.Add("deck", NpgsqlDbType.Jsonb).Value = jsonDeck;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> LogoutUserAsync(string token)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // Check if the token exists
        await using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE token = @token", conn);
        checkCmd.Parameters.AddWithValue("token", token);

        var count = (long) await checkCmd.ExecuteScalarAsync(); // ExecuteScalar returns object, cast to long

        if (count == 0)
        {
            return false; // Token not found, logout failed
        }

        // Invalidate the token by replacing it with a new random value
        await using var updateCmd = new NpgsqlCommand("UPDATE users SET token = @newToken WHERE token = @oldToken", conn);
        updateCmd.Parameters.AddWithValue("newToken", Guid.NewGuid().ToString());
        updateCmd.Parameters.AddWithValue("oldToken", token);

        await updateCmd.ExecuteNonQueryAsync();
        return true; // Logout successful
    }


}