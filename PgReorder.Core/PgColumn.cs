using System.Diagnostics;
using System.Text;

namespace PgReorder.Core;

[DebuggerDisplay("{ColumnName}, OP: {OrdinalPosition}, NOP: {NewOrdinalPosition}")]
public class PgColumn
{
    public required string? ColumnName { get; init; }
    public string? ColumnNameEscaped() => PgShared.Escape(ColumnName);
    public required int OrdinalPosition { get; init; }
    public string? ColumnDefault { get; init; }
    public bool IsNullable { get; init; }
    public string? DataType { get; init; }
    public bool IsIdentity { get; init; }
    
    /// <summary>
    /// 'BY DEFAULT' or 'ALWAYS'
    /// </summary>
    public string? IdentityGeneration { get; init; }
    
    public string? Comments { get; init; }
    
    /// <summary>
    /// Ordinal position after the column is in the new position
    /// </summary>
    public required int NewOrdinalPosition { get; set; }

    /// <summary>
    /// If the new ordinal position is different from the original value, it means this column was moved
    /// </summary>
    public bool WasMoved => OrdinalPosition != NewOrdinalPosition;

    public bool IsPrimaryKey { get; internal set; }
    public bool IsForeignKey { get; internal set; }

    /// <summary>
    /// Visual constraint description (PK for primary key, FK for foreign key)
    /// </summary>
    public string DisplayConstraint => IsPrimaryKey ? "PK" : IsForeignKey ? "FK" : string.Empty;

    /// <summary>
    /// If true, this column was selected in the UI
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Visual indicator that the current column is selected
    /// </summary>
    public string DisplayIsSelected => IsSelected ? "*" : "\xB7";

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