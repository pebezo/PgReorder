using System.Text;

namespace PgReorder.Core;

public class ReorderTableService(DatabaseRepository db)
{
    private PgTable? _table;
    public PgTable LoadedTable => _table ?? throw new Exception("No table has been loaded");
    
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
        
        _table = await db.ReadTable(tableSchema, tableName, token);

        if (_table is null)
        {
            throw new Exception($"Could not read table '{tableSchema}'.'{tableName}'");
        }

        await db.ReadConstraints(_table, token);
    }

    public async Task Save(CancellationToken token)
    {
        var script = GenerateScript();
        await db.Raw(script, token);
    }

    private string GenerateScript()
    {
        var runId = GenerateRandomRunId(6);
        var sourceTable = LoadedTable.SchemaTableEscaped();
        var destinationTable = LoadedTable.SchemaTableEscaped(tableSuffix:$"_reorder_{runId}");
        
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine();
        sb.AppendLine($"CREATE TABLE {destinationTable}");
        sb.AppendLine("(");
        AddCreateLines(sb);
        sb.AppendLine(");");
        sb.AppendLine();

        sb.AppendLine($"LOCK TABLE {sourceTable} IN EXCLUSIVE MODE;");
        sb.AppendLine();

        sb.AppendLine($"INSERT INTO {destinationTable}");
        sb.AppendLine("(");
        AddColumnsInNewOrder(sb);
        sb.AppendLine(")");
        sb.AppendLine("SELECT");
        AddColumnsInNewOrder(sb);
        sb.AppendLine($"FROM {sourceTable};");
        sb.AppendLine();

        sb.AppendLine($"DROP TABLE {sourceTable};");
        sb.AppendLine();

        sb.AppendLine($"ALTER TABLE {destinationTable} RENAME TO {LoadedTable.TableEscaped()};");
        sb.AppendLine();
        
        foreach (var identity in LoadedTable.AllIdentityColumns())
        {
            sb.AppendLine($"-- Configure the identity column {identity.ColumnNameEscaped()}");
            sb.AppendLine($"ALTER TABLE {LoadedTable.TableEscaped()} ALTER {identity.ColumnNameEscaped()} ADD GENERATED {identity.IdentityGeneration} AS IDENTITY;");
            sb.AppendLine("-- Ensure the sequence would return the proper next value");
            sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{LoadedTable.TableEscaped()}', '{identity.ColumnNameEscaped()}'), (SELECT MAX({identity.ColumnNameEscaped()}) FROM {LoadedTable.TableEscaped()}));");
        }
        
        sb.AppendLine("COMMIT TRANSACTION;");
        
        return sb.ToString();
    }

    private void AddCreateLines(StringBuilder sb)
    {
        List<string> lines = [];
        
        foreach (var (column, _) in LoadedTable.Columns())
        {
            lines.Add(column.AppendDefinition());
        }

        foreach (var constraint in LoadedTable.Constraints())
        {
            if (constraint.Definition is not null)
            {
                lines.Add(constraint.Definition);
            }
        }

        for (int i = 0; i < lines.Count; i++)
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
        foreach (var (column, isLast) in LoadedTable.Columns())
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
    
    public void Move(PgColumn column, int position)
    {
        LoadedTable.Move(column, position);
    }

    public void SortInAlphabeticalOrder()
    {
        LoadedTable.SortInAlphabeticalOrder();
    }
    
    public void SortInReverseAlphabeticalOrder()
    {
        LoadedTable.SortInReverseAlphabeticalOrder();
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