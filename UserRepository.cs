using Npgsql;

namespace Monster_Trading_Cards_Game;

public class UserRepository
{
    private readonly string _connectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";
    
    public bool UserExists(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        return (long)cmd.ExecuteScalar() > 0;
    }

    public void InsertUser(string username, string passwordHash, string salt)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (username, password_hash, salt, coins, wins, losses, elo, token)
            VALUES (@username, @passwordhash, @salt, @coins, @wins, @losses, @elo, @token)", conn);

        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("passwordhash", passwordHash);
        cmd.Parameters.AddWithValue("salt", salt);
        cmd.Parameters.AddWithValue("coins", 20);
        cmd.Parameters.AddWithValue("wins", 0);
        cmd.Parameters.AddWithValue("losses", 0);
        cmd.Parameters.AddWithValue("elo", 100);
        cmd.Parameters.AddWithValue("token", ""); 

        cmd.ExecuteNonQuery();
    }

    public (string passwordHash, string salt)? GetUserCredentials(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT password_hash, salt FROM users WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetString(1));
        }

        return null;
    }

    public void UpdateUserToken(string username, string token)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("UPDATE users SET token = @token WHERE username = @username", conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("token", token);

        cmd.ExecuteNonQuery();
    }
}