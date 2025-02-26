using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using PgReorder.Core;
using PgReorder.Core.Configuration;

namespace PgReorder.App;

public class ContextService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SchemaService _schema;
    private readonly ReorderService _reorder;

    public List<PgSchema> Schemas => _schema.Schemas ?? throw new Exception("No schemas have been loaded");
    public List<PgTable> Tables => _schema.Tables ?? throw new Exception("No tables have been loaded");
    public List<PgColumn> Columns => _reorder.Columns ?? throw new Exception("No columns have been loaded"); 
    public PgSchema? SelectedSchema { get; private set; }
    public PgTable? SelectedTable { get; private set; }
    public bool OrderHasChanged => _reorder.OrderHasChanged();
    public string GeneratedScript() => _reorder.GenerateScript();

    [SetsRequiredMembers]
    public ContextService(CommandLineParser parser)
    {
        var serviceProvider = BuildServiceProvider(parser);
        _schema = serviceProvider.GetRequiredService<SchemaService>();
        _reorder = serviceProvider.GetRequiredService<ReorderService>();
    }

    public async Task LoadSchemas()
    {
        await _schema.LoadSchemas(_cts.Token);
    }

    public void SelectSchema(int index)
    {
        SelectedSchema = Schemas[index];
        Task.Run(async () =>
        {
            await _schema.LoadTables(SelectedSchema, _cts.Token);
        }).GetAwaiter().GetResult();
    }

    public void SelectTable(int index)
    {
        SelectedTable = Tables[index];
        Task.Run(async () =>
        {
            await _reorder.Load(SelectedSchema?.SchemaName, SelectedTable?.TableName, _cts.Token);
        }).GetAwaiter().GetResult();
    }

    public (int? first, int? last) FindFirstLastIndex()
    {
        return _reorder.FindFirstLastIndex();
    }
    
    public bool Move(int index, int offset)
    {
        return _reorder.Move(index, offset);
    }

    public void ToggleSelection(int index)
    {
        _reorder.ToggleSelection(index); 
    }
    
    public void ToggleSelection()
    {
        _reorder.ToggleSelection(); 
    }
    
    public void UnselectAll()
    {
        _reorder.UnselectAll(); 
    }
    
    public void SelectAll()
    {
        _reorder.SelectAll(); 
    }
    
    private static ServiceProvider BuildServiceProvider(CommandLineParser parser)
    {
        var services = new ServiceCollection();

        services.AddConfiguration(parser.BuildDatabaseConnection());
        services.AddRepositories();
        services.AddServices();

        var provider = services.BuildServiceProvider();

        return provider;
    }
}