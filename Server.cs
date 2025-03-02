using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    private static readonly Dictionary<string, Func<ParsedRequest, (int, string)>> Routes = new();
    
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
    
    public static void RunServer()
    {
        Console.WriteLine("Initializing database connection...");
        TestDatabaseConnection();
        
        _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080);
        _server.Start();

        Routes["POST:/users"] = User.RegisterUser;
        Routes["POST:/sessions"] = User.LoginUser;
        //Routes["GET:/users/{username}"] = User....
        Routes["POST:/transactions/packages"] = TransactionService.BuyPackage;
        Routes["GET:/cards"] = User.GetCards;
        Routes["GET:/deck"] = User.GetDeck;
        Routes["POST:/deck"] = User.UpdateDeck;
        Routes["GET:/stats"] = User.GetStats;
        Routes["GET:/scoreboard"] = User.GetScoreboard;
        Routes["POST:/logout"] = User.Logout;
        //Routes["POST:/battles"] = EnterLobbyForBattle;
        //Routes["GET:/tradings"] = GetTradingDeals;
        //Routes["DELETE:/tradings/{tradingdealid}"] = Cards....
        
        Console.WriteLine("Server started at http://127.0.0.1:8080");

        while (true)
        {
            var tcpClient = _server.AcceptTcpClient();
            var stream = tcpClient.GetStream();

            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            var parsedRequest = ParseRequest(request);
            Console.WriteLine($"Received Request: {parsedRequest.Method} {parsedRequest.Path}");

            var routeKey = $"{parsedRequest.Method}:{parsedRequest.Path}";
            if (Routes.ContainsKey(routeKey))
            {
                var (statusCode, responseBody) = Routes[routeKey](parsedRequest);
                SendResponse(stream, statusCode, responseBody);
            }
            else
            {
                SendResponse(stream, 404, "{\"error\": \"Not Found\"}");
            }
        }
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
    
    private static void SendResponse(NetworkStream stream, int statusCode, string body)
    {
        var response = $"HTTP/1.1 {statusCode} {GetStatusMessage(statusCode)}\r\n"
                       + "Content-Type: application/json\r\n"
                       + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n"
                       + body;

        var responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
    }

    private static ParsedRequest ParseRequest(string request)
    {
        var parsedRequest = new ParsedRequest();
        var lines = request.Split("\r\n");

        var requestLine = lines[0].Split(' ');
        parsedRequest.Method = requestLine[0];
        parsedRequest.Path = requestLine[1];

        var i = 1;
        while (!string.IsNullOrWhiteSpace(lines[i]))
        {
            var headerParts = lines[i].Split(": ", 2);
            if (headerParts.Length == 2)
            {
                parsedRequest.Headers[headerParts[0]] = headerParts[1];
            }
            i++;
        }

        var bodyStartIndex = request.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        if (bodyStartIndex < request.Length)
        {
            parsedRequest.Body = request[bodyStartIndex..];
        }

        return parsedRequest;
    }

    // Retrieves the users scoreboard ordered by the users ELO
    private static string GetScoreboard(ParsedRequest request)
    {
        throw new NotImplementedException();
    }

    // Enters lobby to start a battle
    private static string EnterLobbyForBattle(ParsedRequest request)
    {
        throw new NotImplementedException();
    }

    // Retrieve currently available trading deals
    private static string GetTradingDeals(ParsedRequest request)
    {
        throw new NotImplementedException();
    }
}