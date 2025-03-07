using System.Text.Json;

namespace Monster_Trading_Cards_Game;

public class TransactionService
{
    private static readonly TransactionRepository TransactionRepo = new TransactionRepository();
    
    private const int PackageCost = 5;

    public static async Task<(int statusCode, string responseBody)> BuyPackageAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var (username, userCoins, userCards) = await TransactionRepo.GetUserByTokenAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            if (userCoins < PackageCost)
            {
                return (403, JsonSerializer.Serialize(new { error = "Not enough coins" }));
            }

            var newCards = await TransactionRepo.GetRandomCardsAsync(5);
            if (newCards.Count < 5)
            {
                return (500, JsonSerializer.Serialize(new { error = "Not enough cards available" }));
            }

            var updatedCards = new List<Card>(userCards);
            updatedCards.AddRange(newCards);

            await TransactionRepo.UpdateUserCardsAsync(username, updatedCards);

            await TransactionRepo.DeductUserCoinsAsync(username, PackageCost);

            return (200, JsonSerializer.Serialize(new { message = "Package purchased successfully", cards = newCards }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }
}
