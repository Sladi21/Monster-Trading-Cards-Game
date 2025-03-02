using System.Text.Json;
using Npgsql;
using System.Collections.Generic;

namespace Monster_Trading_Cards_Game;

public class TransactionRepository
{
    private static readonly string ConnectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";

    // 🔹 Fetch user by token and also get their player_cards JSONB field
    public static (string? username, int coins, List<object> playerCards) GetUserByToken(string token)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT username, coins, player_cards FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        using var reader = cmd.ExecuteReader();
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
    public static List<object> GetRandomCards(int count)
    {
        var cards = new List<object>();

        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT id, name, damage FROM cards ORDER BY RANDOM() LIMIT @count", conn);
        cmd.Parameters.AddWithValue("count", count);

        using var reader = cmd.ExecuteReader();
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
    public static void UpdateUserCards(string username, List<object> updatedCards)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        // 🔹 Convert list to JSON string
        string jsonCards = JsonSerializer.Serialize(updatedCards);

        // 🔹 Update JSONB field (ensure parameter type is set correctly)
        using var cmd = new NpgsqlCommand("UPDATE users SET player_cards = @cards::jsonb WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("cards", NpgsqlTypes.NpgsqlDbType.Jsonb, jsonCards); // Correctly cast as JSONB

        cmd.ExecuteNonQuery();
    }

    // 🔹 Deduct coins from user
    public static void DeductUserCoins(string username, int amount)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("UPDATE users SET coins = coins - @amount WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("username", username);
        cmd.ExecuteNonQuery();
    }
}
