using Npgsql;

namespace Monster_Trading_Cards_Game;

public static class DatabaseConfig
{
    private const string ConnectionString = "Host=localhost;Username=postgres;Password=Passw0rd;Database=mtcg_db";

    public static NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(ConnectionString);
    }
}