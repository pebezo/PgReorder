using PgReorder.Core;

namespace PgReorder.Tests;

public static class TestExtensions
{
    public static PgColumn AddColumn(this Reorder reorder, string columnName)
    {
        var column = new PgColumn
        {
            ColumnName = columnName,
            OrdinalPosition = reorder.Columns.Count + 1,
            NewOrdinalPosition = reorder.Columns.Count + 1
        };
        
        reorder.Columns.Add(column);
        
        return column;
    }
    
    public static void Compare(this Reorder source, Reorder target)
    {
        if (source.Schema?.SchemaName != target.Schema?.SchemaName)
        {
            throw new Exception($"Current schema name '{source.Schema?.SchemaName}' is different from target schema '{target.Schema?.SchemaName}");
        }
    
        if (source.Table?.TableName != target.Table?.TableName)
        {
            throw new Exception($"Current table name '{source.Table}' is different from target table name '{target.Table}");
        }
    
        var columns = target.Columns;
    
        if (source.Columns.Count != columns.Count)
        {
            throw new Exception($"Current table has '{source.Columns.Count}' column(s) versus target with '{columns.Count}' column(s)");
        }
    
        foreach (var sourceColumn in source.Columns)
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
}