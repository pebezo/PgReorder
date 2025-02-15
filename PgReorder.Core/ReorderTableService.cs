using System.Text;

namespace PgReorder.Core;

public class ReorderTableService(DatabaseRepository db)
{
    private ColumnList? _columnList;
    public ColumnList LoadedColumns => _columnList ?? throw new Exception("No table has been loaded");
    
    public string? LastRunId { get; private set; }
    
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
        _columnList = await db.ReadColumns(tableSchema, tableName, token);

        if (_columnList is null)
        {
            throw new Exception($"Could not read table '{tableSchema}'.'{tableName}'");
        }

        await db.ReadConstraints(_columnList, token);
    }

    public async Task Save(CancellationToken token)
    {
        var script = GenerateScript();
        await db.Raw(script, token);
    }

    public string GenerateScript()
    {
        LastRunId = GenerateRandomRunId(6);
        var sourceSchemaAndTable = LoadedColumns.SchemaTableEscaped();
        var destinationSchemaAndTable = LoadedColumns.SchemaTableEscaped(tableSuffix:$"_reorder_{LastRunId}");
        
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine();
        sb.AppendLine($"CREATE TABLE {destinationSchemaAndTable}");
        sb.AppendLine("(");
        AddCreateLines(sb);
        sb.AppendLine(");");
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

        sb.AppendLine($"ALTER TABLE {destinationSchemaAndTable} RENAME TO {LoadedColumns.TableEscaped()};");
        sb.AppendLine();
        
        foreach (var identity in LoadedColumns.AllIdentityColumns())
        {
            sb.AppendLine($"-- Configure the identity column {identity.ColumnNameEscaped()}");
            sb.AppendLine($"ALTER TABLE {LoadedColumns.TableEscaped()} ALTER {identity.ColumnNameEscaped()} ADD GENERATED {identity.IdentityGeneration} AS IDENTITY;");
            sb.AppendLine("-- Ensure the sequence would return the proper next value");
            sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{LoadedColumns.TableEscaped()}', '{identity.ColumnNameEscaped()}'), (SELECT MAX({identity.ColumnNameEscaped()}) FROM {LoadedColumns.TableEscaped()}));");
        }

        foreach (var fk in LoadedColumns.AllForeignKeyConstraints())
        {
            sb.AppendLine();
            sb.AppendLine($"ALTER TABLE {LoadedColumns.TableEscaped()} ADD CONSTRAINT {fk.Name} {fk.Definition};");
        }
        
        sb.AppendLine("COMMIT TRANSACTION;");
        
        return sb.ToString();
    }

    private void AddCreateLines(StringBuilder sb)
    {
        List<string> lines = [];
        
        foreach (var (column, _) in LoadedColumns.ColumnsWithLast())
        {
            lines.Add(column.AppendDefinition());
        }

        foreach (var constraint in LoadedColumns.AllCreateTableConstraints())
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
        foreach (var (column, isLast) in LoadedColumns.ColumnsWithLast())
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
        LoadedColumns.Move(column, position);
    }

    public void SortInAlphabeticalOrder()
    {
        LoadedColumns.SortInAlphabeticalOrder();
    }
    
    public void SortInReverseAlphabeticalOrder()
    {
        LoadedColumns.SortInReverseAlphabeticalOrder();
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