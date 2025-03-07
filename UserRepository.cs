using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Monster_Trading_Cards_Game;

public class UserRepository
{
    public void UpdateStats(User user)
    { 
        using var conn = DatabaseConfig.GetConnection();
        conn.Open();
        
        string jsonDeck = JsonSerializer.Serialize(user.PlayerDeck);
        
        using var cmd = new NpgsqlCommand(@"UPDATE users SET wins = @wins, losses = @losses, elo = @elo, player_deck = @player_deck where username = @username", conn);
        cmd.Parameters.AddWithValue("wins", user.Wins);
        cmd.Parameters.AddWithValue("losses", user.Losses);
        cmd.Parameters.AddWithValue("elo", user.Elo);
        cmd.Parameters.AddWithValue("username", user.Username);
        cmd.Parameters.Add("player_deck", NpgsqlDbType.Jsonb).Value = jsonDeck;
        
        cmd.ExecuteNonQuery();
    }
    
    public async Task<bool> UserExistsAsync(string username)
    {
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        return (long)(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task InsertUserAsync(string username, string passwordHash, string salt)
    {
        await using var conn = DatabaseConfig.GetConnection();
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
        await using var conn = DatabaseConfig.GetConnection();
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
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("UPDATE users SET token = @token WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("token", token);

        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<User?> GetUserByTokenAsync(string token)
    {
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT username, password_hash, salt, coins, wins, losses, elo, token, player_cards, player_deck 
            FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Username = reader.GetString(0),
                PasswordHash = reader.GetString(1),
                Salt = reader.GetString(2),
                Coins = reader.GetInt32(3),
                Wins = reader.GetInt32(4),
                Losses = reader.GetInt32(5),
                Elo = reader.GetInt32(6),
                Token = reader.GetString(7),
                PlayerCards = JsonSerializer.Deserialize<List<Card>>(reader.GetString(8)) ?? new(),
                PlayerDeck = JsonSerializer.Deserialize<List<Card>>(reader.GetString(9)) ?? new()
            };
        }

        return null;
    }
    
    public async Task<List<(string username, int userCoins, int userWins, int userLosses, int userElo)>> GetScoreboardAsync()
    {
        var userStatsList = new List<(string, int, int, int, int)>();
        
        await using var conn = DatabaseConfig.GetConnection();
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
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var fetchCmd = new NpgsqlCommand("SELECT player_cards FROM users WHERE username = @username", conn);
        fetchCmd.Parameters.AddWithValue("username", username);

        string playerCardsJson = (await fetchCmd.ExecuteScalarAsync())?.ToString() ?? "[]";

        var playerCards = JsonSerializer.Deserialize<List<Card>>(playerCardsJson) ?? new List<Card>();

        // Filter the full card objects based on the selected IDs
        var selectedCards = playerCards
            .Where(card => cardIds.Contains(card.Id))
            .ToList();

        await using var cmd = new NpgsqlCommand("UPDATE users SET player_deck = @deck WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        string jsonDeck = JsonSerializer.Serialize(selectedCards);
        cmd.Parameters.Add("deck", NpgsqlDbType.Jsonb).Value = jsonDeck;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> LogoutUserAsync(string token)
    {
        await using var conn = DatabaseConfig.GetConnection();
        conn.Open();

        await using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE token = @token", conn);
        checkCmd.Parameters.AddWithValue("token", token);

        var count = (long) await checkCmd.ExecuteScalarAsync();

        if (count == 0)
        {
            return false;
        }
        
        await using var updateCmd = new NpgsqlCommand("UPDATE users SET token = @newToken WHERE token = @oldToken", conn);
        updateCmd.Parameters.AddWithValue("newToken", Guid.NewGuid().ToString());
        updateCmd.Parameters.AddWithValue("oldToken", token);

        await updateCmd.ExecuteNonQueryAsync();
        return true;
    }
}