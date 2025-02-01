using Microsoft.Extensions.DependencyInjection;

namespace PgReorder.Core.Configuration;

public static class DiRegistration
{
    public static void AddConfiguration(this IServiceCollection services,
        DatabaseConnection databaseConnection)
    {
        services.AddSingleton(databaseConnection);
    }

    public static void AddRepositories(this IServiceCollection services)
    {
        services.AddTransient<DatabaseRepository>();
    }
    
    public static void AddServices(this IServiceCollection services)
    {
        services.AddTransient<ReorderTableService>();
    }
}