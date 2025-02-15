namespace PgReorder.Core;

public class SchemaService(DatabaseRepository db)
{
    public List<PgSchema>? Schemas { get; private set; } = [];
    public List<PgTable>? Tables { get; private set; } = [];

    public int Count => Schemas.Count;
    
    public async Task LoadSchemas(CancellationToken token)
    {
        Schemas = await db.ReadSchemas(token);
    }

    public async Task LoadTables(PgSchema schema, CancellationToken token)
    {
        Tables = await db.ReadTables(schema.SchemaName, token);
    }
}