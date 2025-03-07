using System.Text.Json;

namespace Monster_Trading_Cards_Game;

public class GameEngine(UserRepository repository)
{
    private static readonly object BattleLock = new object();
    private static User? _playerOne = null;
    private static TaskCompletionSource<(User, User, List<string>)>? _waitingTask = null;
    
    public async Task<(int statusCode, string responseBody)> EnterLobbyForBattleAsync(ParsedRequest request)
    {
        try
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Missing or invalid token." }));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await repository.GetUserByTokenAsync(token);
            if (user == null)
            {
                return (401, JsonSerializer.Serialize(new { error = "Unauthorized. Invalid token." }));
            }

            if (user.PlayerDeck == null || !user.PlayerDeck.Any())
            {
                return (400, JsonSerializer.Serialize(new { error = "Invalid request. Player has no deck." }));
            }

            TaskCompletionSource<(User, User, List<string>)>? waitingTask;

            lock (BattleLock)
            {
                if (_playerOne == null)
                {
                    _playerOne = user;
                    _waitingTask = new TaskCompletionSource<(User, User, List<string>)>();

                    Console.WriteLine($"{user.Username} is waiting for a battle...");

                    waitingTask = _waitingTask;
                }
                else
                {
                    User playerOne = _playerOne;
                    User playerTwo = user;

                    Console.WriteLine($"⚔ Battle started: {playerOne.Username} vs {playerTwo.Username}");

                    List<string> battleLog = Battle(playerOne, playerTwo);
                    
                    // Save updated stats
                    repository.UpdateStats(playerOne);
                    repository.UpdateStats(playerTwo);

                    // Notify waiting player
                    _waitingTask?.SetResult((playerOne, playerTwo, battleLog));

                    // Reset state
                    _playerOne = null;
                    _waitingTask = null;

                    return (200, JsonSerializer.Serialize(new
                    {
                        message = "Battle started!",
                        playerOne = new { playerOne.Username },
                        playerTwo = new { playerTwo.Username },
                        battleLog
                    }));
                }
            }

            // Wait for the battle result (for the first player)
            if (waitingTask != null)
            {
                var (player1, player2, battleLog) = await waitingTask.Task;
                return (200, JsonSerializer.Serialize(new
                {
                    message = "Battle started!",
                    playerOne = new { player1.Username },
                    playerTwo = new { player2.Username },
                    battleLog
                }));
            }

            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (500, JsonSerializer.Serialize(new { error = "Internal server error" }));
        }
    }
    
    private List<string> Battle(User player1, User player2) 
    {
        List<string> battleLog = new List<string>();
        Random random = new Random();
        int rounds = 0;
        const int maxRounds = 100;

        // Losing streak tracker for Rage Mode
        int player1LosingStreak = 0;
        int player2LosingStreak = 0;

        // Special Cases
        Dictionary<(string, string), User> specialCases = new()
        {
            { ("Dragon", "Goblin"), player1 },
            { ("Goblin", "Dragon"), player2 },
            { ("Wizzard", "Ork"), player1 },
            { ("Ork", "Wizzard"), player2 },
            { ("WaterSpell", "Knight"), player1 },
            { ("Knight", "WaterSpell"), player2 },
            { ("Kraken", "Spell"), player1 },
            { ("Spell", "Kraken"), player2 },
            { ("FireElf", "Dragon"), player1 },
            { ("Dragon", "FireElf"), player2 }
        };

        // Elemental Effect Multipliers
        Dictionary<(string, string), (double, double)> elementModifiers = new()
        {
            { ("Water", "Fire"), (2.0, 0.5) },
            { ("Fire", "Water"), (0.5, 2.0) },
            { ("Fire", "Normal"), (2.0, 0.5) },
            { ("Normal", "Fire"), (0.5, 2.0) },
            { ("Normal", "Water"), (2.0, 0.5) },
            { ("Water", "Normal"), (0.5, 2.0) }
        };

        while (rounds < maxRounds)
        {
            Card player1Card = player1.PlayerDeck[random.Next(player1.PlayerDeck.Count)];
            Card player2Card = player2.PlayerDeck[random.Next(player2.PlayerDeck.Count)];

            battleLog.Add($"Round {rounds + 1}: {player1.Username} plays {player1Card.Name} (DMG: {player1Card.Damage}, Type: {player1Card.CardType}, Element: {player1Card.Element}) vs. {player2.Username} plays {player2Card.Name} (DMG: {player2Card.Damage}, Type: {player2Card.CardType}, Element: {player2Card.Element})");

            // Apply Rage Mode if a player has lost 3 rounds in a row
            if (player1LosingStreak >= 3)
            {
                player1Card.Damage = (int)(player1Card.Damage * 1.5);
                battleLog.Add($"{player1.Username} enters RAGE MODE! {player1Card.Name} gets +50% damage!");
            }
            if (player2LosingStreak >= 3)
            {
                player2Card.Damage = (int)(player2Card.Damage * 1.5);
                battleLog.Add($"{player2.Username} enters RAGE MODE! {player2Card.Name} gets +50% damage!");
            }

            // Check Special Case Rules
            if (specialCases.TryGetValue((player1Card.Name, player2Card.Name), out User? winner))
            {
                battleLog.Add($"  → Special rule triggered! {player1Card.Name} has a natural advantage over {player2Card.Name}.");
                WinCards(winner.PlayerDeck, winner == player1 ? player2.PlayerDeck : player1.PlayerDeck, winner == player1 ? player2Card : player1Card);
                battleLog.Add($"  → {winner.Username} wins this round!");

                // Reset losing streak of the winner, increase for the loser
                if (winner == player1)
                {
                    player1LosingStreak = 0;
                    player2LosingStreak++;
                }
                else
                {
                    player2LosingStreak = 0;
                    player1LosingStreak++;
                }
            }
            else
            {
                int damage1 = player1Card.Damage;
                int damage2 = player2Card.Damage;

                if (player1Card.CardType != "Monster" || player2Card.CardType != "Monster")
                {
                    if (elementModifiers.TryGetValue((player1Card.Element, player2Card.Element), out var modifier))
                    {
                        damage1 = (int)(damage1 * modifier.Item1);
                        damage2 = (int)(damage2 * modifier.Item2);
                        battleLog.Add($"  → Elemental effect applied: {player1Card.Element} vs {player2Card.Element}.");
                    }
                }

                if (damage1 > damage2)
                {
                    WinCards(player1.PlayerDeck, player2.PlayerDeck, player2Card);
                    battleLog.Add($"  → {player1.Username} wins this round! ({damage1} vs {damage2})");
                    player1LosingStreak = 0;
                    player2LosingStreak++;
                }
                else if (damage2 > damage1)
                {
                    WinCards(player2.PlayerDeck, player1.PlayerDeck, player1Card);
                    battleLog.Add($"  → {player2.Username} wins this round! ({damage2} vs {damage1})");
                    player2LosingStreak = 0;
                    player1LosingStreak++;
                }
                else
                {
                    battleLog.Add($"  → It's a draw! Both players dealt {damage1} damage.");
                }
            }

            rounds++;

            if (player1.PlayerDeck.Count == 0)
            {
                player2.Win();
                player1.Loose();
                battleLog.Add($"{player2.Username} wins the battle! {player1.Username} has no cards left.");
                break;
            }
            if (player2.PlayerDeck.Count == 0)
            {
                player1.Win();
                player2.Loose();
                battleLog.Add($"{player1.Username} wins the battle! {player2.Username} has no cards left.");
                break;
            }
        }

        if (rounds >= maxRounds)
        {
            battleLog.Add("The battle ends in a draw after 100 rounds.");
        }

        return battleLog;
    }

    private void WinCards(List<Card> winnerCards, List<Card> looserCards, Card cardLost)
    {
        winnerCards.Add(cardLost);
        looserCards.Remove(cardLost);
    }
}