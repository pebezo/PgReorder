using System.Diagnostics.CodeAnalysis;

namespace PgReorder.Core;

public class DatabaseConnection
{
    public DatabaseConnection(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public DatabaseConnection(string host, string port, string user, string password, string? database)
    {
        Host = host;
        Port = port;
        User = user;
        Password = password;
        Database = database;
    }
    
    public string? Host { get; init; }
    public string? Port { get; init; }
    public string? User { get; init; }
    public string? Password { get; init; }
    public string? Database { get; init; }

    /// <summary>
    /// Postgres Connection String Info https://www.connectionstrings.com/postgresql/
    /// </summary>
    [field: AllowNull, MaybeNull]
    public string ConnectionString
    {
        get => field ??= $"Server={Host};Port={Port};User Id={User};Password={Password};Database={Database};Command Timeout=300;Include Error Detail=true";
        private set;
    }
}