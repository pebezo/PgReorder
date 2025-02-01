using System.Data;
using Npgsql;

namespace PgReorder.Core;

public class DatabaseRepository(DatabaseConnection connection)
{
    public async Task<bool> DatabaseIsRunning(CancellationToken token)
    {
        try
        {
            await using var source = CreateDataSource();
            await using var cmd = source.CreateCommand();
        
            cmd.CommandText = "SELECT 1;";
            var reader = await cmd.ExecuteScalarAsync(token);

            return (int)(reader ?? 0) == 1;
        }
        catch
        {
            return false;
        }
    }

    public async Task Raw(string sql, CancellationToken token = default)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();
        
        cmd.CommandTimeout = 0;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(token);
    }
    
    public async Task<DataTable> RawDataTable(string sql, CancellationToken token = default)
    {
        await using var cn = new NpgsqlConnection(connection.ConnectionString);
        await using var cmd = new NpgsqlCommand(sql, cn);

        await cn.OpenAsync(token);
        await cmd.PrepareAsync(token);
        var da = new NpgsqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);

        return dt;
    }

    /// <summary>
    /// Using information_schema.columns as a reference starting point:
    /// https://github.com/postgres/postgres/blob/master/src/backend/catalog/information_schema.sql#L667
    /// </summary>
    public async Task<PgTable?> ReadTable(string tableSchema, string tableName, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();

        var table = new PgTable(tableSchema, tableName);

        cmd.Parameters.Add(new NpgsqlParameter("tableSchema", DbType.String) { Value = tableSchema });
        cmd.Parameters.Add(new NpgsqlParameter("tableName", DbType.String) { Value = tableName });
        cmd.CommandText = """
                          SELECT
                              --ns.nspname AS table_schema,
                              --c.relname AS table_name,
                              a.attname column_name,
                              pg_catalog.format_type(a.atttypid, NULL) AS data_type,
                              a.attnum AS ordinal_position,
                              CASE WHEN a.attgenerated = '' THEN PG_GET_EXPR(ad.adbin, ad.adrelid) END AS column_default,
                              NOT(a.attnotnull OR (t.typtype = 'd' AND t.typnotnull)) AS is_nullable,
                              a.attidentity IN ('a', 'd') AS is_identity,
                              CASE a.attidentity WHEN 'a' THEN 'ALWAYS' WHEN 'd' THEN 'BY DEFAULT' END AS identity_generation
                          FROM pg_attribute a
                          JOIN pg_class c ON c.oid = a.attrelid
                          JOIN pg_namespace ns ON c.relnamespace = ns.oid
                          JOIN pg_type t ON t.oid = a.atttypid
                          LEFT JOIN pg_attrdef ad ON a.attrelid = ad.adrelid AND a.attnum = ad.adnum
                          WHERE c.relname = @tableName
                            AND ns.nspname = @tableSchema
                            AND a.attnum > 0
                            AND NOT a.attisdropped
                            AND c.relkind IN ('r', 'v', 'f', 'p')
                          ORDER BY a.attnum
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            var ordinalPosition = ReadStruct<int>(reader, "ordinal_position");
            
            table.AddColumn(new PgColumn
            {
                ColumnName = ReadClass<string>(reader, "column_name"),
                OrdinalPosition = ordinalPosition,
                NewOrdinalPosition = ordinalPosition,
                ColumnDefault = ReadClass<string>(reader, "column_default"),
                IsNullable = ReadStruct<bool>(reader, "is_nullable"),
                DataType = ReadClass<string>(reader, "data_type"),
                //UdtName = ReadClass<string>(reader, "udt_name"),
                IsIdentity = ReadStruct<bool>(reader, "is_identity"),
                IdentityGeneration = ReadClass<string>(reader, "identity_generation")
            });
        }
        
        return table;
    }

    public async Task ReadConstraints(PgTable table, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();

        cmd.Parameters.Add(new NpgsqlParameter("schemaTable", DbType.String) { Value = table.SchemaTableEscaped() });
        cmd.CommandText = """
                          SELECT 
                              c.conname AS constraint_name,
                              c.contype AS constraint_type,
                              pg_get_constraintdef(c.oid) AS constraint_definition
                           FROM pg_constraint c
                          WHERE c.conrelid = @schemaTable::regclass 
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            table.AddConstraint(new PgConstraint
            {
                Name = ReadClass<string>(reader, "constraint_name"),
                Type = ReadStruct<char>(reader, "constraint_type"),
                Definition = ReadClass<string>(reader, "constraint_definition")
            });
        }
    }
    
    private NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connection.ConnectionString);
        builder.EnableDynamicJson();
        return builder.Build();
    }
    
    private T? ReadClass<T>(NpgsqlDataReader pgReader, string field) where T : class
    {
        var ordinal = pgReader.GetOrdinal(field);
        if (pgReader.IsDBNull(ordinal))
        {
            return null;
        }

        return pgReader.GetFieldValue<T>(ordinal);
    }
    
    private T ReadStruct<T>(NpgsqlDataReader pgReader, string field) where T : struct
    {
        var ordinal = pgReader.GetOrdinal(field);
        if (pgReader.IsDBNull(ordinal))
        {
            throw new ArgumentNullException(field, $"Unexpected null value for '{field}'");
        }

        return pgReader.GetFieldValue<T>(ordinal);
    }
}