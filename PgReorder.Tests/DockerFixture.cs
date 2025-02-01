using Microsoft.Extensions.DependencyInjection;
using PgReorder.Core;
using PgReorder.Core.Configuration;
using Serilog;

namespace PgReorder.Tests;

public class DockerFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    
    public DockerFixture()
    {
        // Start a logger so we could see the output from various components in the debug/console window.
        // The built-in methods in XUnit do not seem to work, so this is a workaround.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Debug()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("Starting integration tests");
        
        _serviceProvider = BuildServiceProvider();
        
        // Check if docker is running, if not, throw exception.
        if (!RunBatchFile("docker", "info", false))
        {
            throw new Exception("Docker is not running.");
        }

        RunBatchFile("docker", "compose stop integration-db");
        RunBatchFile("docker", "compose rm -f integration-db");
        RunBatchFile("docker", "compose up -d integration-db");
        
        WaitForPostgres(Build<DatabaseRepository>());
    }

    public void Dispose()
    {
        
    }
    
    public T Build<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddConfiguration(new DatabaseConnection
        {
            Host = "127.0.0.1",
            Port = "8811",
            User = "admin",
            Password = "password",
            Database = "postgres"
        });
        
        services.AddRepositories();
        services.AddServices();
        
        var provider = services.BuildServiceProvider();

        return provider;
    }

    private static bool RunBatchFile(string command, string argument, bool throwException = true)
    {
        OutputMessage($"Running: {command} {argument}");

        try
        {
            Task.Run(async () =>
            {
                await CliWrap.Cli
                    .Wrap(command)
                    .WithArguments(argument)
                    .WithStandardOutputPipe(CliWrap.PipeTarget.ToDelegate(OutputMessage))
                    .WithStandardErrorPipe(CliWrap.PipeTarget.ToDelegate(OutputMessage))
                    .WithValidation(CliWrap.CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync();
            }).GetAwaiter().GetResult();
        }
        catch
        {
            if (throwException)
            {
                throw;
            }

            return false;
        }

        return true;

        void OutputMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Log.Logger.Information(message);
            }
        }
    }

    private static void WaitForPostgres(DatabaseRepository db)
    {
        Task.Run(async () =>
        {
            do
            {
                if (await db.DatabaseIsRunning(CancellationToken.None))
                {
                    Log.Logger.Information("Database is now running");
                    break;
                }

                Log.Logger.Information("Database is not yet running (waiting for 250ms)");
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            } while (true);
        }).GetAwaiter().GetResult();
    }
}