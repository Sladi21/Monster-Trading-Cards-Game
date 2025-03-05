using System.Text.Json;

namespace Monster_Trading_Cards_Game;

public class GameEngine(UserRepository repository)
{
    private static readonly object BattleLock = new object();
    public static string? PlayerOne = null;
    public static List<object>? PlayerOneDeck = null;
    public static string? PlayerTwo = null;
    public static List<object>? PlayerTwoDeck = null;
    private static TaskCompletionSource<(string, string, string)>? _waitingTask = null;

    public async Task <(int statusCode, string responseBody)> EnterLobbyForBattleAsync(ParsedRequest request)
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
            var (username, userDeck) = await repository.GetUserDeckAsync(token);
            if (username == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }
            // TODO: Other errors for empty deck
            
            TaskCompletionSource<(string, string, string)>? waitingTask;
            
            lock (BattleLock)
            {
                if (PlayerOne == null)
                {
                    PlayerOne = username;
                    PlayerOneDeck = userDeck;
                    _waitingTask = new TaskCompletionSource<(string, string, string)>();
                    
                    Console.WriteLine($"{username} is waiting for a battle...");
                    
                    waitingTask = _waitingTask;
                }
                else
                {
                    // start battle
                    PlayerTwo = username;
                    PlayerTwoDeck = userDeck;
                
                    // Battle
                    string winner = Battle(PlayerOne, PlayerOneDeck, PlayerTwo, PlayerTwoDeck);
                    string loser = winner == PlayerOne ? PlayerTwo : PlayerOne;
                    
                    
                    // 🔹 Notify waiting player (first player gets result)
                    _waitingTask.SetResult((PlayerOne, PlayerTwo, winner));
                    _waitingTask = null; // Reset for next battles
                
                    PlayerOne = null;
                    PlayerOneDeck = null;
                    
                    //return (200, JsonSerializer.Serialize(new { message = "Starting battle..." }));
                    return (200, JsonSerializer.Serialize(new
                    {
                        message = "Battle started!",
                        PlayerOne,
                        PlayerTwo,
                        winner,
                        loser
                    }));
                }
            }
            var (player1Name, player2Name, battleWinner) = await waitingTask!.Task;

            return (200, JsonSerializer.Serialize(new
            {
                message = "Battle started!",
                player1 = player1Name,
                player2 = player2Name,
                winner = battleWinner
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }

    public string Battle(string player1, List<object> player1Deck, string player2, List<object> player2Deck)
    {
        return player1;
    }
    
}