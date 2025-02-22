namespace PgReorder.Core;

public class PgTable
{
    public required string TableName { get; init; }
    public required string Owner { get; init; }
    public string[]? Options { get; init; }
}