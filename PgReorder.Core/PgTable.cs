namespace PgReorder.Core;

public class PgTable(string? schema, string? table)
{
    private readonly List<PgColumn> _columns = [];
    private readonly List<PgConstraint> _constraints = [];
    
    public string? Schema { get; } = schema;
    public string? SchemaEscaped() => PgShared.Escape(Schema);
    
    public string? Table { get; } = table;
    public string? TableEscaped(string? tableSuffix = null) => tableSuffix is null
        ? PgShared.Escape(Table)
        : PgShared.Escape(Table + tableSuffix);
    
    public string SchemaTableEscaped(string? tableSuffix = null) => $"{SchemaEscaped()}.{TableEscaped(tableSuffix)}";
    
    public IEnumerable<(PgColumn Column, bool IsLast)> Columns()
    {
        for (int i = 0; i < _columns.Count; i++)
        {
            yield return (_columns[i], i == _columns.Count - 1);
        }
    }

    public IEnumerable<PgColumn> AllIdentityColumns()
    {
        foreach (var column in _columns)
        {
            if (column.IsIdentity)
            {
                yield return column;
            }
        }
    }

    public IEnumerable<PgConstraint> Constraints() => _constraints;
    
    public void AddColumn(PgColumn column)
    {
        _columns.Add(column);
    }
    
    public PgColumn AddColumn(string columnName)
    {
        var column = new PgColumn
        {
            ColumnName = columnName,
            OrdinalPosition = _columns.Count + 1,
            NewOrdinalPosition = _columns.Count + 1
        };
        
        _columns.Add(column);
        
        return column;
    }
    
    public void AddConstraint(PgConstraint constraint)
    {
        _constraints.Add(constraint);
    }

    public PgColumn? FindColumn(string? columnName)
    {
        return _columns.Find(p => p.ColumnName == columnName);
    }
    
    public PgColumn GetColumn(string? columnName)
    {
        return _columns.Find(p => p.ColumnName == columnName)
               ?? throw new Exception($"Could not find column '{columnName}'");
    }
    
    public bool Move(PgColumn column, int position)
    {
        return Move(column.ColumnName, position);
    }
    
    public bool Move(string? name, int position)
    {
        var target = _columns.Find(p => p.ColumnName == name);
        if (target is null)
        {
            throw new Exception($"Could not find column with name '{name}'");
        }

        var index = _columns.IndexOf(target);
        if (index > -1)
        {
            _columns.RemoveAt(index);
            var newIndex = index + position;

            if (newIndex < 0 || newIndex > _columns.Count)
            {
                return false;
            }
            
            _columns.Insert(newIndex, target);

            UpdateNewOrdinalPositions();
        }

        return true;
    }

    public void SortInAlphabeticalOrder()
    {
        _columns.Sort((p1, p2) => string.Compare(p1.ColumnName, p2.ColumnName, StringComparison.OrdinalIgnoreCase));
        UpdateNewOrdinalPositions();
    }
    
    public void SortInReverseAlphabeticalOrder()
    {
        _columns.Sort((p1, p2) => -string.Compare(p1.ColumnName, p2.ColumnName, StringComparison.OrdinalIgnoreCase));
        UpdateNewOrdinalPositions();
    }

    public void Compare(PgTable target)
    {
        if (Schema != target.Schema)
        {
            throw new Exception($"Current schema name '{Schema}' is different from target schema '{target.Schema}");
        }

        if (Table != target.Table)
        {
            throw new Exception($"Current table name '{Table}' is different from target table name '{target.Table}");
        }

        var columns = target.Columns().ToList();

        if (_columns.Count != columns.Count)
        {
            throw new Exception($"Current table has '{_columns.Count}' column(s) versus target with '{columns.Count}' column(s)");
        }

        foreach (var sourceColumn in _columns)
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
        foreach (var item in _columns)
        {
            item.NewOrdinalPosition = ++ordinalPosition;
        }
    }
}