namespace Monster_Trading_Cards_Game;

internal static class Program
{
    static async Task Main(string[] args)
    {
        await Server.RunServerAsync();
    }
}