using System.Text;

namespace PgReorder.Core;

public class ReorderService(DatabaseRepository db) : Reorder
{
    public string? LastRunId { get; private set; }
    public string? LastScript { get; private set; }
    
    public async Task Load(string? tableSchema, string? tableName, CancellationToken token)
    {
        if (tableSchema is null)
        {
            throw new ArgumentNullException(nameof(tableSchema), "Schema cannot be null");
        }
        
        if (tableName is null)
        {
            throw new ArgumentNullException(nameof(tableName), "Table name cannot be null");
        }

        BeforeLoad();
        
        (Schema, Table) = await db.ReadSchemaAndTable(tableSchema, tableName, token);
        
        var taskReadColumns = db.ReadColumns(this, token);
        var taskReadConstraints = db.ReadConstraints(this, token);
        var taskReadIndexes = db.ReadIndexes(this, token);
        
        Task.WaitAll(taskReadColumns, taskReadConstraints, taskReadIndexes);
        
        AfterLoad();
    }

    public async Task Save(CancellationToken token)
    {
        LastScript = GenerateScript();
        await db.Raw(LastScript, token);
    }

    public string GenerateScript()
    {
        LastRunId = GenerateRandomRunId(6);
        var suffix = $"_reorder_{LastRunId}";
        var sourceSchemaAndTable = SchemaTableEscaped();
        var destinationSchemaAndTable = SchemaTableEscaped(tableSuffix: suffix);
        
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine();
        
        AddCreateDestinationTable(sb, destinationSchemaAndTable);
        AddTableOptions(sb);
        sb.AppendLine(";");
        AddTableComments(sb, destinationSchemaAndTable);
        AddColumnComments(sb, destinationSchemaAndTable);
        sb.AppendLine();

        sb.AppendLine("-- Lock the original table to prevent data loss");
        sb.AppendLine($"LOCK TABLE {sourceSchemaAndTable} IN EXCLUSIVE MODE;");
        sb.AppendLine();

        AddInsertIntoDestinationTable(sb, destinationSchemaAndTable, sourceSchemaAndTable);
        sb.AppendLine();

        sb.AppendLine("-- Drop the original table since all the existing data has been copied over");
        sb.AppendLine($"DROP TABLE {sourceSchemaAndTable};");
        sb.AppendLine();

        sb.AppendLine("-- Rename the new table back to the original name");
        sb.AppendLine($"ALTER TABLE {destinationSchemaAndTable} RENAME TO {Table?.TableNameEscaped};");
        sb.AppendLine();

        ConfigureIdentityColumns(sb);
        RenamePrimaryKeyConstraints(sb, sourceSchemaAndTable, suffix);
        AddForeignKeyConstraints(sb);
        AddIndexes(sb);
        
        sb.AppendLine("COMMIT TRANSACTION;");
        
        return sb.ToString();
    }

    private void AddTableOptions(StringBuilder sb)
    {
        if (Table?.Options is not null && Table.Options.Length > 0)
        {
            foreach (var (item, first, last) in Iterate(Table.Options))
            {
                if (first)
                {
                    sb.AppendLine();
                    sb.AppendLine("WITH");
                    sb.AppendLine("(");        
                }
                sb.Append("    ");
                if (last)
                {
                    sb.AppendLine(item);
                    sb.Append(')');
                }
                else
                {
                    sb.Append(item);
                    sb.AppendLine(",");
                }
            }
        }
    }

    private void AddTableComments(StringBuilder sb, string destinationSchemaAndTable)
    {
        if (Table?.Comments is not null)
        {
            sb.AppendLine($"COMMENT ON TABLE {destinationSchemaAndTable} IS '{PgShared.EscapeQuotes(Table.Comments)}';");
        }
    }
    
    private void AddColumnComments(StringBuilder sb, string destinationSchemaAndTable)
    {
        if (Columns.Any(p => p.Comments is not null))
        {
            foreach (var column in Columns)
            {
                if (column.Comments is not null)
                {
                    sb.AppendLine($"COMMENT ON COLUMN {destinationSchemaAndTable}.{column.ColumnNameEscaped()} IS '{PgShared.EscapeQuotes(column.Comments)}';");
                }
            }
        }
    }
    
    private void ConfigureIdentityColumns(StringBuilder sb)
    {
        foreach (var (identity, first, last) in Iterate(AllIdentityColumns()))
        {
            if (first)
            {
                sb.AppendLine("-- Configure identity column(s)");
            }

            sb.AppendLine($"ALTER TABLE {Table?.TableNameEscaped} ALTER {identity.ColumnNameEscaped()} ADD GENERATED {identity.IdentityGeneration} AS IDENTITY;");
            // Ensure the sequence would return the proper next value
            // ReSharper disable once StringLiteralTypo
            sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{Table?.TableNameEscaped}', '{identity.ColumnNameEscaped()}'), (SELECT MAX({identity.ColumnNameEscaped()}) FROM {Table?.TableNameEscaped}));");

            if (last)
            {
                sb.AppendLine();
            }
        }
    }
    
    private void RenamePrimaryKeyConstraints(StringBuilder sb, string schemaAndTable, string suffix)
    {
        var constraints = AllPrimaryKeyConstraints().ToList();
        if (constraints.Count > 1)
        {
            throw new Exception($"Unexpected number of primary key constraints ({constraints.Count})");
        }

        if (constraints.Count == 1)
        {
            sb.AppendLine("-- Rename the generated primary key constraint name to match the original name");
            sb.AppendLine($"ALTER TABLE {schemaAndTable} RENAME CONSTRAINT {Table?.TableNameEscaped}{suffix}_pkey TO {constraints[0].Name};");
            sb.AppendLine();
        }
    }
    
    private void AddForeignKeyConstraints(StringBuilder sb)
    {
        foreach (var (fk, first, last) in Iterate(AllForeignKeyConstraints()))
        {
            if (first)
            {
                sb.AppendLine("-- Add back foreign key(s)");
            }
            
            sb.AppendLine($"ALTER TABLE {Table?.TableNameEscaped} ADD CONSTRAINT {fk.Name} {fk.Definition};");
            
            if (last)
            {
                sb.AppendLine();
            }
        }
    }

    private void AddIndexes(StringBuilder sb)
    {
        foreach (var (index, first, last) in Iterate(Indexes))
        {
            if (first)
            {
                sb.AppendLine("-- Add back index(es)");
            }
            
            sb.Append(index.Definition);
            sb.AppendLine(";");
            
            if (last)
            {
                sb.AppendLine();
            }
        }
    }

    private void AddCreateDestinationTable(StringBuilder sb, string destinationSchemaAndTable)
    {
        sb.AppendLine("-- Create a new table with the new column order");
        sb.AppendLine($"CREATE TABLE {destinationSchemaAndTable}");
        sb.AppendLine("(");
        
        List<string> lines = [];
        
        foreach (var column in Columns)
        {
            lines.Add(column.AppendDefinition());
        }

        foreach (var constraint in AllPrimaryKeyConstraints())
        {
            if (constraint.Definition is not null)
            {
                lines.Add(constraint.Definition);
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            sb.Append("    ");
            sb.Append(lines[i]);
            if (i < lines.Count - 1)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine();
            }
        }
        
        sb.Append(')');
    }

    private void AddInsertIntoDestinationTable(StringBuilder sb, string destinationSchemaAndTable, string sourceSchemaAndTable)
    {
        sb.AppendLine("-- Copy existing data into the temporary table");
        sb.AppendLine($"INSERT INTO {destinationSchemaAndTable}");
        sb.AppendLine("(");
        AddColumnsInNewOrder();
        sb.AppendLine(")");
        sb.AppendLine("SELECT");
        AddColumnsInNewOrder();
        sb.AppendLine($"FROM {sourceSchemaAndTable};");
        return;

        void AddColumnsInNewOrder()
        {
            foreach (var (column, _, isLast) in Iterate(Columns))
            {
                sb.Append($"    {column.ColumnNameEscaped()}");
                if (!isLast)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }
        }
    }

    private static IEnumerable<(T item, bool isFirst, bool isLast)> Iterate<T>(IEnumerable<T> items)
    {
        return Iterate(items.ToList());
    }
    
    private static IEnumerable<(T item, bool isFirst, bool isLast)> Iterate<T>(IReadOnlyList<T> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            yield return (items[i], i == 0, i == items.Count - 1);
        }
    }
    
    private static string GenerateRandomRunId(int size)
    {
        // ReSharper disable once StringLiteralTypo
        const string CHARS = "abcdefghijklmnoprstuvwxyz0123456789";
        var vin = new char[size];

        for (var i = 0; i < vin.Length; i++)
        {
            vin[i] = CHARS[Random.Shared.Next(CHARS.Length)];
        }

        return new string(vin);
    }
}