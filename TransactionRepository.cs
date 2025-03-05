using System.Text.Json;
using Npgsql;
using System.Collections.Generic;

namespace Monster_Trading_Cards_Game;

public class TransactionRepository
{
    private static readonly string ConnectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";

    // 🔹 Fetch user by token and also get their player_cards JSONB field
    public static async Task<(string? username, int coins, List<object> playerCards)> GetUserByTokenAsync(string token)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT username, coins, player_cards FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.Read())
        {
            var username = reader.GetString(0);
            var coins = reader.GetInt32(1);
            var cardsJson = reader.GetString(2);

            var playerCards = JsonSerializer.Deserialize<List<object>>(cardsJson) ?? new List<object>();

            return (username, coins, playerCards);
        }

        return (null, 0, new List<object>());
    }

    // 🔹 Fetch 5 random cards from the `cards` table
    public static async Task<List<object>> GetRandomCardsAsync(int count)
    {
        var cards = new List<object>();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT id, name, damage FROM cards ORDER BY RANDOM() LIMIT @count", conn);
        cmd.Parameters.AddWithValue("count", count);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            var card = new
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Damage = reader.GetFloat(2)
            };

            cards.Add(card);
        }

        return cards;
    }

    // 🔹 Update user's player_cards JSONB field
    public static async Task UpdateUserCardsAsync(string username, List<object> updatedCards)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // 🔹 Convert list to JSON string
        string jsonCards = JsonSerializer.Serialize(updatedCards);

        // 🔹 Update JSONB field (ensure parameter type is set correctly)
        await using var cmd = new NpgsqlCommand("UPDATE users SET player_cards = @cards::jsonb WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("cards", NpgsqlTypes.NpgsqlDbType.Jsonb, jsonCards); // Correctly cast as JSONB

        await cmd.ExecuteNonQueryAsync();
    }

    // 🔹 Deduct coins from user
    public static async Task DeductUserCoinsAsync(string username, int amount)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("UPDATE users SET coins = coins - @amount WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("username", username);
        await cmd.ExecuteNonQueryAsync();
    }
}
