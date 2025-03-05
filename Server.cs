using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Monster_Trading_Cards_Game;

public class ParsedRequest
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; } = new();
    public string Body { get; set; } = string.Empty;
}

public static class Server
{
    private static TcpListener? _server;
    private static readonly Dictionary<string, Func<ParsedRequest, Task<(int, string)>>> Routes = new();

    private static void TestDatabaseConnection()
    {
        try
        {
            using var conn = DatabaseConfig.GetConnection();
            conn.Open();
            Console.WriteLine("Database connection successful!");
        }
        catch (NpgsqlException ex)
        {
            Console.WriteLine($"Database connection failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task RunServerAsync()
    {
        UserRepository userRepository = new UserRepository();
        GameEngine gameEngine = new(userRepository);

        Console.WriteLine("Initializing database connection...");
        TestDatabaseConnection();

        _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080);
        _server.Start();

        Routes["POST:/users"] = User.RegisterUserAsync;
        Routes["POST:/sessions"] = User.LoginUserAsync;
        Routes["POST:/transactions/packages"] = TransactionService.BuyPackageAsync;
        Routes["GET:/cards"] = User.GetCardsAsync;
        Routes["GET:/deck"] = User.GetDeckAsync;
        Routes["POST:/deck"] = User.UpdateDeckAsync;
        Routes["GET:/stats"] = User.GetStatsAsync;
        Routes["GET:/scoreboard"] = User.GetScoreboardAsync;
        Routes["POST:/logout"] = User.LogoutAsync;
        Routes["POST:/battles"] = gameEngine.EnterLobbyForBattleAsync;

        Console.WriteLine("Server started at http://127.0.0.1:8080");

        while (true)
        {
            var tcpClient = await _server.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(tcpClient)); // Handle client concurrently
        }
    }

    private static async Task HandleClientAsync(TcpClient tcpClient)
    {
        using (tcpClient)
        {
            var stream = tcpClient.GetStream();
            var request = await ReadRequestAsync(stream);

            var parsedRequest = ParseRequest(request);
            Console.WriteLine($"Received Request: {parsedRequest.Method} {parsedRequest.Path}");

            var routeKey = $"{parsedRequest.Method}:{parsedRequest.Path}";
            if (Routes.ContainsKey(routeKey))
            {
                var (statusCode, responseBody) = await Routes[routeKey](parsedRequest);
                await SendResponseAsync(stream, statusCode, responseBody);
            }
            else
            {
                await SendResponseAsync(stream, 404, "{\"error\": \"Not Found\"}");
            }
        }
    }

    private static async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string body)
    {
        var response = $"HTTP/1.1 {statusCode} {GetStatusMessage(statusCode)}\r\n"
                       + "Content-Type: application/json\r\n"
                       + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n"
                       + body;

        var responseBytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
    }

    private static string GetStatusMessage(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            500 => "Internal Server Error",
            _ => "Unknown"
        };
    }

    private static ParsedRequest ParseRequest(string request)
    {
        var lines = request.Split("\r\n");
        var firstLineParts = lines[0].Split(' ');

        var parsedRequest = new ParsedRequest
        {
            Method = firstLineParts[0],
            Path = firstLineParts[1]
        };

        int i = 1;
        while (i < lines.Length && lines[i] != "")
        {
            var headerParts = lines[i].Split(": ");
            if (headerParts.Length == 2)
            {
                parsedRequest.Headers[headerParts[0]] = headerParts[1];
            }
            i++;
        }

        parsedRequest.Body = string.Join("\n", lines.Skip(i + 1));
        return parsedRequest;
    }
}
