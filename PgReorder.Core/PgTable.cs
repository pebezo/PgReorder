namespace PgReorder.Core;

public class PgTable
{
    public required string? Tablespace { get; init; }
    public required string TableName { get; init; }
    public required string Owner { get; init; }
    public string[]? Options { get; init; }
    public string? Comments { get; init; }

    public string? TableNameEscaped => PgShared.Escape(TableName);

    public string? TableNameEscapedWithSuffix(string? suffix) => suffix is null
        ? PgShared.Escape(TableName)
        : PgShared.Escape(TableName + suffix);
}