namespace PgReorder.Core;

public class Reorder
{
    public PgSchema? Schema { get; protected set; }
    public PgTable? Table { get; protected set; }
    public List<PgColumn> Columns { get; } = [];
    public List<PgConstraint> Constraints { get; }= [];
    public List<PgIndex> Indexes { get; } = [];

    protected IEnumerable<PgColumn> AllIdentityColumns() => Columns.Where(p => p.IsIdentity);
    protected IEnumerable<PgConstraint> AllPrimaryKeyConstraints() => Constraints.Where(c => c.IsPrimaryKey);
    public IEnumerable<PgConstraint> AllForeignKeyConstraints() => Constraints.Where(c => c.IsForeignKey);
    
    public string SchemaTableEscaped(string? tableSuffix = null) => $"{Schema?.SchemaNameEscaped}.{Table?.TableNameEscapedWithSuffix(tableSuffix)}";
    
    internal void AfterColumnLoad()
    {
        foreach (var column in Columns)
        {
            column.IsPrimaryKey = IsPrimaryKey(column);
            column.IsForeignKey = IsForeignKey(column);
        }
    }

    private bool IsPrimaryKey(PgColumn column)
    {
        return Constraints.Any(p => p.ColumnNames is not null && 
                                     p.ColumnNames.Any(c => c == column.ColumnName)  
                                     && p.IsPrimaryKey);
    }

    private bool IsForeignKey(PgColumn column)
    {
        return Constraints.Any(p => p.ColumnNames is not null && 
                                     p.ColumnNames.Any(c => c == column.ColumnName)  
                                     && p.IsForeignKey);
    }

    public PgColumn? FindColumn(string? columnName)
    {
        return Columns.Find(p => p.ColumnName == columnName);
    }
    
    public (int? first, int? last) FindFirstLastIndex()
    {
        int? first = null;
        int? last = null;

        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].IsSelected)
            {
                first ??= i;
                last = i;
            }
        }

        return (first, last);
    }
   
    public bool Move(PgColumn column, int offset)
    {
        return Move(column.ColumnName, offset);
    }
    
    /// <summary>
    /// Move specific column identified by name
    /// </summary>
    public bool Move(string? name, int offset)
    {
        var target = Columns.Find(p => p.ColumnName == name);
        if (target is null)
        {
            throw new Exception($"Could not find column with name '{name}'");
        }

        var index = Columns.IndexOf(target);
        if (index > -1)
        {
            return Move([index], offset);
        }

        return false;
    }
    
    /// <summary>
    /// Move selected items by offset if there are any, or use the selected index as a fallback. 
    /// </summary>
    public bool Move(int selectedIndex, int offset)
    {
        // If we have one or more selections it should take precedence over the currently selected item
        if (HasSelection())
        {
            return Move(GetSelection(), offset); 
        }

        // If we don't have any column that's selected we should move the currently selected item
        return Move([selectedIndex], offset);
    }

    /// <summary>
    /// Move selected items by offset. 
    /// </summary>
    public bool Move(int offset)
    {
        if (!HasSelection())
        {
            return false;
        }

        return Move(GetSelection(), offset);
    }
    
    private bool Move(List<int> indexes, int offset)
    {
        // Ensure that every index is within bounds. We should never have a case where the index is outside. 
        if (indexes.Any(i => i < 0 || i >= Columns.Count))
        {
            throw new ArgumentException("One or more indexes are out of bounds.", nameof(indexes));
        }

        // If any of the columns would end up outside of bounds we should not allow the move to continue  
        if (indexes.Any(i => i + offset < 0 || i + offset >= Columns.Count))
        {
            return false;
        }
        
        var orderedIndexes = offset >= 0 
            ? indexes.OrderByDescending(i => i)  // Process from right to left when moving down
            : indexes.OrderBy(i => i);           // Process from left to right when moving up
        
        foreach (var index in orderedIndexes)
        {
            var newPosition = index + offset;

            // All of these cases should've been caught above, but in case we missed one.
            if (newPosition < 0 || newPosition > Columns.Count - 1)
            {
                throw new Exception($"Index {index} would be moved outside of bounds {newPosition} [0..{Columns.Count - 1}]");
            }

            var item = Columns[index];
            Columns.RemoveAt(index);
            Columns.Insert(newPosition, item);
        }
        
        UpdateNewOrdinalPositions();

        return true;
    }
    
    /// <summary>
    /// Returns true is any of the columns were moved
    /// </summary>
    public bool OrderHasChanged()
    {
        return Columns.Count > 0 && Columns.Any(p => p.WasMoved);
    }
    
    /// <summary>
    /// Returns true if we have one or more columns that have been selected to be moved
    /// </summary>
    private bool HasSelection()
    {
        return Columns.Any(p => p.IsSelected);
    }
    
    /// <summary>
    /// Returns a list of column indexes that have been selected 
    /// </summary>
    private List<int> GetSelection()
    {
        List<int> indexes = [];

        for (int i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].IsSelected)
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    public void ToggleSelection(int index)
    {
        Columns[index].IsSelected = !Columns[index].IsSelected;
    }
    
    public void ToggleSelection()
    {
        foreach (var column in Columns)
        {
            column.IsSelected = !column.IsSelected;
        }
    }

    public void UnselectAll()
    {
        foreach (var column in Columns)
        {
            column.IsSelected = false;
        }
    }
    
    public void SelectAll()
    {
        foreach (var column in Columns)
        {
            column.IsSelected = true;
        }
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
    
    /// <summary>
    /// Update the new ordinal position of every column using the current list order
    /// </summary>
    private void UpdateNewOrdinalPositions()
    {
        var ordinalPosition = 0;
        foreach (var item in Columns)
        {
            item.NewOrdinalPosition = ++ordinalPosition;
        }
    }
}