namespace PgReorder.Core;

public class PgSchema
{
    public required string SchemaName { get; init; }
    public required string Owner { get; init; }

    public string? SchemaNameEscaped => PgShared.Escape(SchemaName);
}