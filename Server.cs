using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Monster_Trading_Cards_Game;

internal class ParsedRequest
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string HttpVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    public string Body { get; set; } = string.Empty;
}
public static class Server
{
    private static TcpListener? _server;
    private static readonly Dictionary<string, User> Users = new();
    private static readonly Dictionary<string, Func<ParsedRequest, string>> Routes = new();

    public static void RunServer()
    {
        _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080);
        _server.Start();

        Routes["POST:/register"] = RegisterUser;
        Routes["POST:/login"] = LoginUser;

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
            var responseBody = Routes.ContainsKey(routeKey) 
                ? Routes[routeKey](parsedRequest) 
                : "{\"error\": \"Not Found\"}";

            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\n\r\n{responseBody}";
            var responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }

    private static ParsedRequest ParseRequest(string request)
    {
        var parsedRequest = new ParsedRequest();
        var lines = request.Split("\r\n");

        var requestLine = lines[0].Split(' ');
        parsedRequest.Method = requestLine[0];
        parsedRequest.Path = requestLine[1];
        parsedRequest.HttpVersion = requestLine[2];

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

    private static string RegisterUser(ParsedRequest request)
    {
        Console.WriteLine($"Request Body: {request.Body}");

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return "{\"error\": \"Request body is empty\"}";
        }

        try
        {
            var userData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);

            if (userData == null || !userData.ContainsKey("username") || !userData.ContainsKey("password"))
            {
                return "{\"error\": \"Invalid data\"}";
            }

            var username = userData["username"];
            var password = userData["password"];

            if (Users.ContainsKey(username))
            {
                return "{\"error\": \"User already exists\"}";
            }

            Users[username] = new User { Username = username, Password = password };
            return "{\"message\": \"User registered successfully\"}";
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.WriteLine($"JSON Parsing Error: {ex.Message}");
            return "{\"error\": \"Invalid JSON format\"}";
        }
    }

    private static string LoginUser(ParsedRequest request)
    {
        var loginData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body);
        if (loginData == null || !loginData.ContainsKey("username") || !loginData.ContainsKey("password"))
        {
            return "{\"error\": \"Invalid data\"}";
        }

        var username = loginData["username"];
        var password = loginData["password"];

        if (!Users.ContainsKey(username) || Users[username].Password != password)
        {
            return "{\"error\": \"Invalid username or password\"}";
        }

        var token = GenerateToken();
        Users[username].Token = token;

        return $"{{\"message\": \"Login successful\", \"token\": \"{token}\"}}";
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}