using System.Reflection;
using Terminal.Gui;

namespace PgReorder.App;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionCaption = $"v{version?.Major ?? 1}.{version?.Minor ?? 0}";
            
            var parser = new CommandLineParser(args);

            if (parser.NothingGiven)
            {
                Console.WriteLine($"PgReorder {versionCaption}");
                Console.WriteLine("---------");
                Console.WriteLine();
                Console.WriteLine("Usage: pgreorder [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --cs \"Server=localhost;Port=5000;User Id=my_user;Password=my_password;Database=my_database;\"");
                Console.WriteLine("  --host localhost");
                Console.WriteLine("  --port 5000");
                Console.WriteLine("  --user my_user");
                Console.WriteLine("  --password my_password");
                Console.WriteLine("  --database my_database");
                Console.WriteLine("  --schema my_schema");
                Console.WriteLine();
                Console.WriteLine("With 'cs' you can specify the entire connection string using this format:");
                Console.WriteLine("https://www.connectionstrings.com/postgresql/");
                Console.WriteLine();
                Console.WriteLine("If you specify 'cs' then you do not need to specify the rest of the options. However, if 'cn' is missing, then the host, port, user, password, and database must be specified.");
                return 0;
            }

            var context = new ContextService(parser);
            await context.LoadSchemas();
            
            
            Application.Init();
            Application.Run(new MainWindow(context, versionCaption));
            Application.Shutdown();
        }
        catch (Exception e)
        {
            if (args.Contains("--verbose", StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(e.ToString());
            }
            else
            {
                Console.Error.WriteLine(e.Message);    
            }

#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#endif

            return 1;
        }

        return 0;
    }
}