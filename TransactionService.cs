using System.Text.Json;
using System.Collections.Generic;

namespace Monster_Trading_Cards_Game;

public class TransactionService
{
    private const int PackageCost = 20;

    public static (int statusCode, string responseBody) BuyPackage(ParsedRequest request)
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
            var (username, userCoins, userCards) = TransactionRepository.GetUserByToken(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            // 🔹 Check if user has enough coins
            if (userCoins < PackageCost)
            {
                return (403, JsonSerializer.Serialize(new { error = "Not enough coins" }));
            }

            // 🔹 Get 5 random cards
            var newCards = TransactionRepository.GetRandomCards(5);
            if (newCards.Count < 5)
            {
                return (500, JsonSerializer.Serialize(new { error = "Not enough cards available" }));
            }

            // 🔹 Merge new cards with user's existing cards
            var updatedCards = new List<object>(userCards);
            updatedCards.AddRange(newCards);

            // 🔹 Assign new cards to user in JSONB format
            TransactionRepository.UpdateUserCards(username, updatedCards);

            // 🔹 Deduct coins
            TransactionRepository.DeductUserCoins(username, PackageCost);

            return (200, JsonSerializer.Serialize(new { message = "Package purchased successfully", cards = newCards }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }
}
