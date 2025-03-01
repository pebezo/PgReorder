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

    public async Task<List<PgSchema>> ReadSchemas(CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();
        
        cmd.CommandText = """
                          SELECT ns.nspname AS schema_name, 
                                 r.rolname AS owner_name
                          FROM pg_catalog.pg_namespace ns
                          JOIN pg_roles r ON ns.nspowner = r.oid
                          WHERE ns.nspname NOT LIKE 'pg_%'
                            AND ns.nspname <> 'information_schema'
                          ORDER BY ns.nspname
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        var schemas = new List<PgSchema>();
        
        while (await reader.ReadAsync(token))
        {
            schemas.Add(new PgSchema
            {
                SchemaName = ReadClass<string>(reader, "schema_name") ?? throw new Exception("Unexpected 'null' schema name"),
                Owner = ReadClass<string>(reader, "owner_name") ?? throw new Exception("Unexpected 'null' schema owner")
            });
        }

        return schemas;
    }
    
    public async Task<List<PgTable>> ReadTables(string schema, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();
        
        cmd.Parameters.Add(new NpgsqlParameter("schemaName", DbType.String) { Value = schema });
        cmd.CommandText = """
                          SELECT    
                              c.relname table_name,
                              pg_catalog.pg_get_userbyid(c.relowner) table_owner
                          FROM pg_catalog.pg_class c
                          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                          WHERE c.relkind = 'r'      -- regular table
                            AND NOT c.relispartition -- exclude child partitions
                            AND n.nspname = @schemaName
                          ORDER  BY table_name
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        var schemas = new List<PgTable>();
        
        while (await reader.ReadAsync(token))
        {
            schemas.Add(new PgTable
            {
                TableName = ReadClass<string>(reader, "table_name") ?? throw new Exception("Unexpected 'null' table name"),
                Owner = ReadClass<string>(reader, "table_owner")  ?? throw new Exception("Unexpected 'null' table owner")
            });
        }

        return schemas;
    }
    
    public async Task<(PgSchema schema, PgTable table)> ReadSchemaAndTable(string schema, string table, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();
        
        cmd.Parameters.Add(new NpgsqlParameter("schemaName", DbType.String) { Value = schema });
        cmd.Parameters.Add(new NpgsqlParameter("tableName", DbType.String) { Value = table });
        cmd.CommandText = """
                          SELECT    
                              n.nspname schema_name,
                              r.rolname AS schema_owner,
                              c.relname table_name,
                              pg_catalog.pg_get_userbyid(c.relowner) table_owner,
                              c.reloptions table_options,
                              pg_catalog.obj_description(c.oid) table_comments
                          FROM pg_catalog.pg_class c
                          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                          JOIN pg_roles r ON n.nspowner = r.oid
                          WHERE c.relkind = 'r'      -- regular table
                            AND NOT c.relispartition -- exclude child partitions
                            AND n.nspname = @schemaName
                            AND c.relname = @tableName
                          ORDER  BY table_name
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            var pgSchema = new PgSchema
            {
                SchemaName = ReadClass<string>(reader, "schema_name") ?? throw new Exception("Unexpected 'null' schema name"),
                Owner = ReadClass<string>(reader, "schema_owner") ?? throw new Exception("Unexpected 'null' schema owner")
            };
            
            var pgTable = new PgTable
            {
                TableName = ReadClass<string>(reader, "table_name") ?? throw new Exception("Unexpected 'null' table name"),
                Owner = ReadClass<string>(reader, "table_owner")  ?? throw new Exception("Unexpected 'null' table owner"),
                Options = ReadClass<string[]?>(reader, "table_options"),
                Comments = ReadClass<string?>(reader, "table_comments")
            };

            return (pgSchema, pgTable);
        }

        throw new Exception($"Could not load {schema}.{table}");
    }
    
    /// <summary>
    /// Using information_schema.columns as a reference starting point:
    /// https://github.com/postgres/postgres/blob/master/src/backend/catalog/information_schema.sql#L667
    /// </summary>
    public async Task ReadColumns(Reorder reorder, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();
        
        cmd.Parameters.Add(new NpgsqlParameter("schemaName", DbType.String) { Value = reorder.Schema?.SchemaName });
        cmd.Parameters.Add(new NpgsqlParameter("tableName", DbType.String) { Value = reorder.Table?.TableName });
        cmd.CommandText = """
                          SELECT
                              a.attname column_name,
                              pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                              a.attnum AS ordinal_position,
                              CASE WHEN a.attgenerated = '' THEN PG_GET_EXPR(ad.adbin, ad.adrelid) END AS column_default,
                              NOT(a.attnotnull OR (t.typtype = 'd' AND t.typnotnull)) AS is_nullable,
                              a.attidentity IN ('a', 'd') AS is_identity,
                              CASE a.attidentity WHEN 'a' THEN 'ALWAYS' WHEN 'd' THEN 'BY DEFAULT' END AS identity_generation,
                              pg_catalog.col_description(c.oid, a.attnum) AS column_comments
                          FROM pg_attribute a
                          JOIN pg_class c ON c.oid = a.attrelid
                          JOIN pg_namespace ns ON c.relnamespace = ns.oid
                          JOIN pg_type t ON t.oid = a.atttypid
                          LEFT JOIN pg_attrdef ad ON a.attrelid = ad.adrelid AND a.attnum = ad.adnum
                          WHERE c.relname = @tableName
                            AND ns.nspname = @schemaName
                            AND a.attnum > 0
                            AND NOT a.attisdropped
                            AND c.relkind IN ('r', 'v', 'f', 'p')
                          ORDER BY a.attnum
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            var ordinalPosition = ReadStruct<int>(reader, "ordinal_position");
            
            reorder.Columns.Add(new PgColumn
            {
                ColumnName = ReadClass<string>(reader, "column_name"),
                OrdinalPosition = ordinalPosition,
                NewOrdinalPosition = ordinalPosition,
                ColumnDefault = ReadClass<string>(reader, "column_default"),
                IsNullable = ReadStruct<bool>(reader, "is_nullable"),
                DataType = ReadClass<string>(reader, "data_type"),
                IsIdentity = ReadStruct<bool>(reader, "is_identity"),
                IdentityGeneration = ReadClass<string>(reader, "identity_generation"),
                Comments = ReadClass<string>(reader, "column_comments")
            });
        }
    }

    public async Task ReadConstraints(Reorder reorder, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();

        cmd.Parameters.Add(new NpgsqlParameter("schemaName", DbType.String) { Value = reorder.Schema?.SchemaName });
        cmd.Parameters.Add(new NpgsqlParameter("tableName", DbType.String) { Value = reorder.Table?.TableName });
        cmd.CommandText = """
                          SELECT 
                              c.conname AS constraint_name,
                              c.contype AS constraint_type,
                              pg_get_constraintdef(c.oid) AS constraint_definition,
                              ARRAY_AGG(a.attname) AS column_name
                          FROM pg_constraint c
                          INNER JOIN pg_namespace ns ON ns.oid = c.connamespace
                          CROSS JOIN LATERAL unnest(c.conkey) ak(k)
                          INNER JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ak.k
                          WHERE c.conrelid::regclass::text = @tableName
                            AND ns.nspname = @schemaName
                          GROUP BY 1,2,3
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            reorder.Constraints.Add(new PgConstraint
            {
                Name = ReadClass<string>(reader, "constraint_name"),
                Type = ReadStruct<char>(reader, "constraint_type"),
                Definition = ReadClass<string>(reader, "constraint_definition"),
                ColumnNames = ReadClass<string[]>(reader, "column_name")
            });
        }
    }

    public async Task ReadIndexes(Reorder reorder, CancellationToken token)
    {
        await using var source = CreateDataSource();
        await using var cmd = source.CreateCommand();

        cmd.Parameters.Add(new NpgsqlParameter("schemaName", DbType.String) { Value = reorder.Schema?.SchemaName });
        cmd.Parameters.Add(new NpgsqlParameter("tableName", DbType.String) { Value = reorder.Table?.TableName });
        cmd.CommandText = """
                          SELECT
                              x.indexrelid::regclass::text AS index_name,
                              pg_get_indexdef(i.oid) AS index_definition
                          FROM pg_index x
                          JOIN pg_class c ON c.oid = x.indrelid
                          JOIN pg_class i ON i.oid = x.indexrelid
                          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                          WHERE n.nspname = @schemaName
                            AND c.relname = @tableName
                            AND c.relkind IN ('r', 'm', 'p')
                            AND i.relkind IN ('i', 'I')
                            AND NOT x.indisprimary
                          """;
        
        await using var reader = await cmd.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
        {
            reorder.Indexes.Add(new PgIndex
            {
                Name = ReadClass<string>(reader, "index_name"),
                Definition = ReadClass<string>(reader, "index_definition")
            });
        }
    }
    
    private NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connection.ConnectionString);
        return builder.Build();
    }
    
    private static T? ReadClass<T>(NpgsqlDataReader pgReader, string field) where T : class?
    {
        var ordinal = pgReader.GetOrdinal(field);
        if (pgReader.IsDBNull(ordinal))
        {
            return null;
        }

        return pgReader.GetFieldValue<T>(ordinal);
    }
    
    private static T ReadStruct<T>(NpgsqlDataReader pgReader, string field) where T : struct
    {
        var ordinal = pgReader.GetOrdinal(field);
        if (pgReader.IsDBNull(ordinal))
        {
            throw new ArgumentNullException(field, $"Unexpected null value for '{field}'");
        }

        return pgReader.GetFieldValue<T>(ordinal);
    }
}