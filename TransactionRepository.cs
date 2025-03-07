using System.Text.Json;
using Npgsql;

namespace Monster_Trading_Cards_Game;

public class TransactionRepository
{
    public async Task<(string? username, int coins, List<Card> playerCards)> GetUserByTokenAsync(string token)
    {
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT username, coins, player_cards FROM users WHERE token = @token", conn);
        cmd.Parameters.AddWithValue("token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.Read())
        {
            var username = reader.GetString(0);
            var coins = reader.GetInt32(1);
            var cardsJson = reader.GetString(2);

            var playerCards = JsonSerializer.Deserialize<List<Card>>(cardsJson) ?? new List<Card>();

            return (username, coins, playerCards);
        }

        return (null, 0, new List<Card>());
    }

    public async Task<List<Card>> GetRandomCardsAsync(int count)
    {
        var cards = new List<Card>();

        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT id, name, damage, element, card_type FROM cards ORDER BY RANDOM() LIMIT @count", conn);
        cmd.Parameters.AddWithValue("count", count);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            var card = new Card(
                reader.GetInt32(0),  // id
                reader.GetString(1), // name
                (int)reader.GetDouble(2),  // damage
                reader.GetString(3), // element
                reader.GetString(4)  // card_type
            );

            cards.Add(card);
        }

        return cards;
    }

    public async Task UpdateUserCardsAsync(string username, List<Card> updatedCards)
    {
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        string jsonCards = JsonSerializer.Serialize(updatedCards);

        await using var cmd = new NpgsqlCommand("UPDATE users SET player_cards = @cards::jsonb WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("cards", NpgsqlTypes.NpgsqlDbType.Jsonb, jsonCards);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeductUserCoinsAsync(string username, int amount)
    {
        await using var conn = DatabaseConfig.GetConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("UPDATE users SET coins = coins - @amount WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("username", username);
        await cmd.ExecuteNonQueryAsync();
    }
}
