using PgReorder.Core;

namespace PgReorder.App;

public class CommandLineParser
{
    public string? ConnectionString { get; private set; }
    public string? Host { get;  private set; }
    public string? Port { get;  private set; }
    public string? User { get;  private set; }
    public string? Password { get;  private set; }
    public string? Database { get;  private set; }
    public bool NothingGiven { get; private set; }

    public CommandLineParser(string[] args)
    {
        Parse(args);
    }

    public DatabaseConnection BuildDatabaseConnection()
    {
        if (ConnectionString is not null)
        {
            return new DatabaseConnection(ConnectionString);
        }

        return new DatabaseConnection(
            Host ?? throw new Exception("Host was not specified"),
            Port ?? "5432",
            User ?? throw new Exception("User was not specified"),
            Password ?? throw new Exception("Password was not specified"),
            Database ?? "postgres"
        );
    }

    private void Parse(string[] args)
    {
        if (args.Length == 0)
        {
            NothingGiven = true;
            return;
        }
        
        var index = -1;
        while (++index < args.Length)
        {
            var key = args[index];
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception($"Unexpected null command line argument #{index}");
            }

            if (key.Equals("--cs", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("--connection", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("--connection-string", StringComparison.OrdinalIgnoreCase))
            {
                ConnectionString = Next($"Missing connection string after '{key}'");
                continue;
            }

            if (key.Equals("--host", StringComparison.OrdinalIgnoreCase))
            {
                Host = Next($"Missing host after '{key}'");
                continue;
            }
            
            if (key.Equals("--port", StringComparison.OrdinalIgnoreCase))
            {
                Port = Next($"Missing port after '{key}");
                continue;
            }
            
            if (key.Equals("--user", StringComparison.OrdinalIgnoreCase))
            {
                User = Next($"Missing user after '{key}");
                continue;
            }
            
            if (key.Equals("--password", StringComparison.OrdinalIgnoreCase))
            {
                Password = Next($"Missing password after '{key}");
                continue;
            }
            
            if (key.Equals("--database", StringComparison.OrdinalIgnoreCase))
            {
                Database = Next($"Missing database after '{key}");
                continue;
            }
            
            if (key.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            throw new Exception($"Unexpected command line option: {key}");
        }
        
        return;

        string Next(string errorMessage)
        {
            if (++index >= args.Length)
            {
                throw new Exception(errorMessage);
            }

            return args[index];
        }
    }
}