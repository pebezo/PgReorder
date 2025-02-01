using System.Diagnostics.CodeAnalysis;

namespace PgReorder.Core;

public class DatabaseConnection
{
    public required string Host { get; init; }
    public required string Port { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public required string Database { get; init; }
    
    /// <summary>
    /// Postgres Connection String Info https://www.connectionstrings.com/postgresql/
    /// </summary>
    [field: AllowNull, MaybeNull]
    public string ConnectionString => field ??= $"Server={Host};Port={Port};User Id={User};Password={Password};Database={Database};Command Timeout=300;Include Error Detail=true";
}