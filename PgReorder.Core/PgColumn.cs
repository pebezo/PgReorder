using System.Text;

namespace PgReorder.Core;

public class PgColumn
{
    public required string? ColumnName { get; set; }
    public string? ColumnNameEscaped() => PgShared.Escape(ColumnName);
    public required int OrdinalPosition { get; set; }
    public string? ColumnDefault { get; set; }
    public bool IsNullable { get; set; }
    public string? DataType { get; set; }
    public bool IsIdentity { get; set; }
    
    /// <summary>
    /// 'BY DEFAULT' or 'ALWAYS'
    /// </summary>
    public string? IdentityGeneration { get; set; }
    
    /// <summary>
    /// Ordinal position after the column is in the new position
    /// </summary>
    public required int NewOrdinalPosition { get; set; }

    public string AppendDefinition()
    {
        var sb = new StringBuilder();
        
        sb.Append(ColumnNameEscaped());
        sb.Append(' ');
        sb.Append(DataType);
        if (IsNullable is false)
        {
            sb.Append(" NOT NULL");
        }

        return sb.ToString();
    }
}