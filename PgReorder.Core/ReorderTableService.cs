using System.Text;

namespace PgReorder.Core;

public class ReorderTableService(DatabaseRepository db)
{
    public PgTable? Table { get; private set; }
    private ColumnList? _columns;
    public ColumnList Columns => _columns ?? throw new Exception("No table has been loaded");
    
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

        LastRunId = null;
        Table = await db.ReadTable(tableSchema, tableName, token);
        _columns = await db.ReadColumns(tableSchema, tableName, token);

        if (_columns is null)
        {
            throw new Exception($"Could not read table '{tableSchema}'.'{tableName}'");
        }

        await db.ReadConstraints(_columns, token);
        
        _columns.AfterColumnLoad();
    }

    public async Task Save(CancellationToken token)
    {
        LastScript = GenerateScript();
        await db.Raw(LastScript, token);
    }

    public bool OrderHasChanged()
    {
        return Columns.Columns.Count > 0 && Columns.Columns.Any(p => p.WasMoved);
    }

    public string GenerateScript()
    {
        LastRunId = GenerateRandomRunId(6);
        var sourceSchemaAndTable = Columns.SchemaTableEscaped();
        var destinationSchemaAndTable = Columns.SchemaTableEscaped(tableSuffix:$"_reorder_{LastRunId}");
        
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine();
        sb.AppendLine($"CREATE TABLE {destinationSchemaAndTable}");
        sb.AppendLine("(");
        AddCreateLines(sb);
        sb.Append(")");
        AddTableOptions(sb);
        sb.AppendLine(";");
        AddTableComments(sb, destinationSchemaAndTable);
        AddColumnComments(sb, destinationSchemaAndTable);
        sb.AppendLine();

        sb.AppendLine($"LOCK TABLE {sourceSchemaAndTable} IN EXCLUSIVE MODE;");
        sb.AppendLine();

        sb.AppendLine($"INSERT INTO {destinationSchemaAndTable}");
        sb.AppendLine("(");
        AddColumnsInNewOrder(sb);
        sb.AppendLine(")");
        sb.AppendLine("SELECT");
        AddColumnsInNewOrder(sb);
        sb.AppendLine($"FROM {sourceSchemaAndTable};");
        sb.AppendLine();

        sb.AppendLine($"DROP TABLE {sourceSchemaAndTable};");
        sb.AppendLine();

        sb.AppendLine($"ALTER TABLE {destinationSchemaAndTable} RENAME TO {Columns.TableEscaped()};");
        sb.AppendLine();
        
        foreach (var identity in Columns.AllIdentityColumns())
        {
            sb.AppendLine($"-- Configure the identity column {identity.ColumnNameEscaped()}");
            sb.AppendLine($"ALTER TABLE {Columns.TableEscaped()} ALTER {identity.ColumnNameEscaped()} ADD GENERATED {identity.IdentityGeneration} AS IDENTITY;");
            sb.AppendLine("-- Ensure the sequence would return the proper next value");
            sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{Columns.TableEscaped()}', '{identity.ColumnNameEscaped()}'), (SELECT MAX({identity.ColumnNameEscaped()}) FROM {Columns.TableEscaped()}));");
        }

        foreach (var fk in Columns.AllForeignKeyConstraints())
        {
            sb.AppendLine();
            sb.AppendLine($"ALTER TABLE {Columns.TableEscaped()} ADD CONSTRAINT {fk.Name} {fk.Definition};");
        }
        
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
        if (Columns.Columns.Any(p => p.Comments is not null))
        {
            foreach (var column in Columns.Columns)
            {
                if (column.Comments is not null)
                {
                    sb.AppendLine($"COMMENT ON COLUMN {destinationSchemaAndTable}.{column.ColumnNameEscaped()} IS '{PgShared.EscapeQuotes(column.Comments)}';");
                }
            }
        }
    }

    private void AddCreateLines(StringBuilder sb)
    {
        List<string> lines = [];
        
        foreach (var column in Columns.Columns)
        {
            lines.Add(column.AppendDefinition());
        }

        foreach (var constraint in Columns.AllCreateTableConstraints())
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
    }

    private void AddColumnsInNewOrder(StringBuilder sb)
    {
        foreach (var (column, _, isLast) in Iterate(Columns.Columns))
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

    public void SortInAlphabeticalOrder()
    {
        Columns.SortInAlphabeticalOrder();
    }
    
    public void SortInReverseAlphabeticalOrder()
    {
        Columns.SortInReverseAlphabeticalOrder();
    }

    private IEnumerable<(T item, bool isFirst, bool isLast)> Iterate<T>(IReadOnlyList<T> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            yield return (items[i], i == 0, i == items.Count - 1);
        }
    }
    
    private static string GenerateRandomRunId(int size)
    {
        const string CHARS = "abcdefghijklmnoprstuvwxyz0123456789";
        var vin = new char[size];

        for (int i = 0; i < vin.Length; i++)
        {
            vin[i] = CHARS[Random.Shared.Next(CHARS.Length)];
        }

        return new string(vin);
    }
}