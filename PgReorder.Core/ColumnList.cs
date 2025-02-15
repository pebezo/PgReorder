namespace PgReorder.Core;

public class ColumnList(string? schema, string? table)
{
    private readonly List<PgConstraint> _constraints = [];
    
    public string? Schema { get; } = schema;
    public string? SchemaEscaped() => PgShared.Escape(Schema);
    
    public string? Table { get; } = table;
    public string? TableEscaped(string? tableSuffix = null) => tableSuffix is null
        ? PgShared.Escape(Table)
        : PgShared.Escape(Table + tableSuffix);
    
    public string SchemaTableEscaped(string? tableSuffix = null) => $"{SchemaEscaped()}.{TableEscaped(tableSuffix)}";
 
    public List<PgColumn> Columns { get; } = [];
    public IEnumerable<(PgColumn Column, bool IsLast)> ColumnsWithLast()
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            yield return (Columns[i], i == Columns.Count - 1);
        }
    }

    public IEnumerable<PgColumn> AllIdentityColumns()
    {
        foreach (var column in Columns)
        {
            if (column.IsIdentity)
            {
                yield return column;
            }
        }
    }

    /// <summary>
    /// List of constraints that should be used during the CREATE TABLE DDL
    /// </summary>
    public IEnumerable<PgConstraint> AllCreateTableConstraints() => _constraints.Where(c => c.UseInCreateTable);
    
    /// <summary>
    /// List of constraints that should be used after the temporary table has been dropped
    /// </summary>
    public IEnumerable<PgConstraint> AllForeignKeyConstraints() => _constraints.Where(c => !c.UseInCreateTable);
    
    public void AddColumn(PgColumn column)
    {
        Columns.Add(column);
    }
    
    public PgColumn AddColumn(string columnName)
    {
        var column = new PgColumn
        {
            ColumnName = columnName,
            OrdinalPosition = Columns.Count + 1,
            NewOrdinalPosition = Columns.Count + 1
        };
        
        Columns.Add(column);
        
        return column;
    }
    
    public void AddConstraint(PgConstraint constraint)
    {
        _constraints.Add(constraint);
    }

    public PgColumn? FindColumn(string? columnName)
    {
        return Columns.Find(p => p.ColumnName == columnName);
    }
    
    public PgColumn GetColumn(string? columnName)
    {
        return Columns.Find(p => p.ColumnName == columnName)
               ?? throw new Exception($"Could not find column '{columnName}'");
    }
    
    public bool Move(PgColumn column, int position)
    {
        return Move(column.ColumnName, position);
    }
    
    public bool Move(string? name, int position)
    {
        var target = Columns.Find(p => p.ColumnName == name);
        if (target is null)
        {
            throw new Exception($"Could not find column with name '{name}'");
        }

        var index = Columns.IndexOf(target);
        if (index > -1)
        {
            Columns.RemoveAt(index);
            var newIndex = index + position;

            if (newIndex < 0 || newIndex > Columns.Count)
            {
                return false;
            }
            
            Columns.Insert(newIndex, target);

            UpdateNewOrdinalPositions();
        }

        return true;
    }

    public void SortInAlphabeticalOrder()
    {
        Columns.Sort((p1, p2) => string.Compare(p1.ColumnName, p2.ColumnName, StringComparison.OrdinalIgnoreCase));
        UpdateNewOrdinalPositions();
    }
    
    public void SortInReverseAlphabeticalOrder()
    {
        Columns.Sort((p1, p2) => -string.Compare(p1.ColumnName, p2.ColumnName, StringComparison.OrdinalIgnoreCase));
        UpdateNewOrdinalPositions();
    }

    public void Compare(ColumnList target)
    {
        if (Schema != target.Schema)
        {
            throw new Exception($"Current schema name '{Schema}' is different from target schema '{target.Schema}");
        }

        if (Table != target.Table)
        {
            throw new Exception($"Current table name '{Table}' is different from target table name '{target.Table}");
        }

        var columns = target.ColumnsWithLast().ToList();

        if (Columns.Count != columns.Count)
        {
            throw new Exception($"Current table has '{Columns.Count}' column(s) versus target with '{columns.Count}' column(s)");
        }

        foreach (var sourceColumn in Columns)
        {
            var found = target.FindColumn(sourceColumn.ColumnName);
            if (found is null)
            {
                throw new Exception($"Could not find column '{sourceColumn.ColumnName}' in target table name '{target.Table}'");
            }

            if (sourceColumn.ColumnDefault != found.ColumnDefault)
            {
                throw new Exception($"Column default '{sourceColumn.ColumnDefault}' is different ('{found.ColumnDefault}') in target table name '{target.Table}'");
            }
            
            if (sourceColumn.IsNullable != found.IsNullable)
            {
                throw new Exception($"Column nullability '{sourceColumn.IsNullable}' is different ('{found.IsNullable}') in target table name '{target.Table}'");
            }
            
            if (sourceColumn.DataType != found.DataType)
            {
                throw new Exception($"Column data type '{sourceColumn.DataType}' is different ('{found.DataType}') in target table name '{target.Table}'");
            }
            
            if (sourceColumn.IdentityGeneration != found.IdentityGeneration)
            {
                throw new Exception($"Column identity generation '{sourceColumn.IdentityGeneration}' is different ('{found.IdentityGeneration}') in target table name '{target.Table}'");
            }
        }
    }

    private void UpdateNewOrdinalPositions()
    {
        // Reorder columns
        var ordinalPosition = 0;
        foreach (var item in Columns)
        {
            item.NewOrdinalPosition = ++ordinalPosition;
        }
    }
}