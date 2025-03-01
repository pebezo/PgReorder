using Xunit;

namespace PgReorder.Tests;

public class IsolatedTableTests(DockerFixture fixture) : DockerBase(fixture)
{
    /// <summary>
    /// Test a really simple use-case to make sure we're catching potential issues without additional complexity
    /// </summary>
    [Fact]
    public async Task Simple_Table_With_Data()
    {
        var table = $"simple_{TestId}";
        await Db.Raw($"CREATE TABLE public.{table} (c1 integer, c2 integer, c3 integer);");
        await Db.Raw($"INSERT INTO public.{table} (c1, c2, c3) VALUES (1,2,3);");
        await Db.Raw($"INSERT INTO public.{table} (c1, c2, c3) VALUES (4,5,6);");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);

        rs.Move("c1", +2);
        rs.Move("c2", +1);

        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // The second row (c1 = 4) should return columns in this order: c3, c3, c1
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY c1 DESC LIMIT 1", table, 6, 5, 4);
    }
    
    /// <summary>
    /// Make sure the script properly escapes schema, table, and column names
    /// </summary>
    [Fact]
    public async Task Schema_Table_And_Columns_With_Spaces()
    {
        var schema = "schema with space";
        var table = $"sample with space {TestId}";
        await Db.Raw($"CREATE SCHEMA \"{schema}\";");
        await Db.Raw($"CREATE TABLE \"{schema}\".\"{table}\" (c1 integer, \"c2 with space\" integer, c3 integer);");
        await Db.Raw($"INSERT INTO \"{schema}\".\"{table}\" (c1, \"c2 with space\", c3) VALUES (1,2,3);");
        await Db.Raw($"INSERT INTO \"{schema}\".\"{table}\" (c1, \"c2 with space\", c3) VALUES (4,5,6);");
        
        var rs = ReorderService;
        await rs.Load(schema, table, CancellationToken.None);

        rs.Move("c1", +2);
        rs.Move("c2 with space", +1);

        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // The second row (c1 = 4) should return columns in this order: c3, c2, c1
        await CheckRowValues($"SELECT * FROM \"{schema}\".\"{table}\" ORDER BY c1 DESC LIMIT 1", table, 6, 5, 4);
    }
    
    /// <summary>
    /// Make sure the script properly escapes reserved words
    /// </summary>
    [Fact]
    public async Task Schema_Table_And_Columns_With_Reserved_Words()
    {
        var schema = "ANY";
        var table = "USER";
        await Db.Raw($"CREATE SCHEMA \"{schema}\";");
        await Db.Raw($"CREATE TABLE \"{schema}\".\"{table}\" (c1 integer, \"AS\" integer, c3 integer);");
        await Db.Raw($"INSERT INTO \"{schema}\".\"{table}\" (c1, \"AS\", c3) VALUES (1,2,3);");
        await Db.Raw($"INSERT INTO \"{schema}\".\"{table}\" (c1, \"AS\", c3) VALUES (4,5,6);");
        
        var rs = ReorderService;
        await rs.Load(schema, table, CancellationToken.None);

        rs.Move("c1", +2);
        rs.Move("AS", +1);

        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // The second row (c1 = 4) should return columns in this order: c3, c2, c1
        await CheckRowValues($"SELECT * FROM \"{schema}\".\"{table}\" ORDER BY c1 DESC LIMIT 1", table, 6, 5, 4);
    }

    /// <summary>
    /// Ensure that most common data types, and a few custom ones, are properly transferred over to the new table
    /// </summary>
    [Fact]
    public async Task Various_Data_Types()
    {
        var table = $"data_types{TestId}";
        await Db.Raw(
            $"""
             CREATE TYPE mood AS ENUM ('sad', 'ok', 'happy');
             CREATE TYPE complex AS (
                 r       double precision,
                 i       double precision
             );
             CREATE DOMAIN posint AS integer CHECK (VALUE > 0);
             CREATE TABLE public.{table}
             (
                 c1 smallint,
                 c2 integer,
                 c3 bigint,
                 c4 decimal,
                 c5 numeric(3, -1),
                 c6 real, 
                 c7 double precision,
                 c8 money,
                 c9 timestamp,
                 d1 date,
                 d2 time,
                 d3 interval,
                 d4 boolean,
                 d5 mood,
                 d6 integer[2][2],
                 d7 json,
                 d8 jsonb,
                 d9 complex,
                 e1 posint
             );
             """);

        await Db.Raw($$$"""
                     INSERT INTO public.{{{table}}} (
                        c1, c2, c3, c4, c5, c6, c7, c8, c9,
                        d1, d2, d3, d4, d5, d6, 
                        d7, d8, d9,
                        e1
                     )
                     VALUES (
                        1, 2, 3, 4.1, 2.01, 0.677, 2.1415, 41.24,
                        '1999-01-08 04:05:06 -8:00', '2025-1-31', '15:41', '1-2', true, 'happy', '{{1,2},{3,4}}',
                        '{"bar": "baz"}'::json, '{"bar2": "baz2"}'::jsonb, ROW(2.41, 4.73),
                        123
                     )
                     """);
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        
        rs.SortInReverseAlphabeticalOrder();
        
        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
    }

    [Fact]
    public async Task Generate_Always_As_Identity()
    {
        var table = $"generate_always_as_identity{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id)
             );
             """);
        
        await Db.Raw($"INSERT INTO public.{table} (c1, c2) VALUES (10, 11)");
        await Db.Raw($"INSERT INTO public.{table} (c1, c2) VALUES (20, 21)");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        rs.Move("c2", -1);
        
        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // Add a new row after reordering to ensure that `id` receives the correct next value
        await Db.Raw($"INSERT INTO public.{table} (c1, c2) VALUES (30, 31)");

        // Expecting: id = 3, c2 = 31, c1 = 30
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 3, 31, 30);
    }
    
    [Fact]
    public async Task Generate_By_Default_As_Identity()
    {
        var table = $"generate_by_default_as_identity{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL GENERATED BY DEFAULT AS IDENTITY,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id)
             );
             """);
        
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (1, 10, 11)");
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (2, 20, 21)");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        rs.Move("id", +1);
        
        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // Add a new row after reordering to ensure that `id` receives the correct next value
        await Db.Raw($"INSERT INTO public.{table} (c1, c2) VALUES (30, 31)");

        // Expecting: c1 = 30, id = 3, c2 = 31 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 30, 3, 31);
    }
    
    [Fact]
    public async Task Table_With_One_Primary_Key_Column()
    {
        var table = $"one_pk_{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id)
             );
             """);
        
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (1, 10, 11)");
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (2, 20, 21)");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        rs.Move("id", +1);
        
        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // Expecting: c1 = 30, id = 3, c2 = 31 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 20, 2, 21);
    }
    
    [Fact]
    public async Task Table_With_Multiple_Primary_Key_Columns()
    {
        var table = $"multiple_pk_{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id1 integer NOT NULL,
                 id2 integer NOT NULL,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id1, id2)
             );
             """);
        
        await Db.Raw($"INSERT INTO public.{table} (id1, id2, c1, c2) VALUES (1, 200, 10, 11)");
        await Db.Raw($"INSERT INTO public.{table} (id1, id2, c1, c2) VALUES (2, 201, 20, 21)");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        rs.Move("c1", +1);
        
        await rs.Save(CancellationToken.None);
        
        await CheckColumnDefinition(rs);
        
        // Expecting: id1 = 2, id2 = 201, c2 = 21, c1 = 20 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id1 DESC LIMIT 1", table, 2, 201, 21, 20);
    }

    [Fact]
    public async Task Table_With_Options()
    {
        var table = $"with_options_{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id)
             )
             WITH
             (
                autovacuum_enabled = TRUE,
                autovacuum_analyze_scale_factor = 0.2,
                autovacuum_analyze_threshold = 5000
             )
             ;
             """);
        
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (1, 10, 11)");
        await Db.Raw($"INSERT INTO public.{table} (id, c1, c2) VALUES (2, 20, 21)");
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        rs.Move("id", +2);
        
        await rs.Save(CancellationToken.None);
        
        Assert.Contains("autovacuum_enabled", rs.LastScript);
        Assert.Contains("autovacuum_analyze_scale_factor", rs.LastScript);
        Assert.Contains("autovacuum_analyze_threshold", rs.LastScript);
        
        await CheckColumnDefinition(rs);
        
        // Expecting: id1 = 2, id2 = 201, c2 = 21, c1 = 20 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 20, 21, 2);
    }
    
    [Fact]
    public async Task Table_And_Columns_With_Comments()
    {
        var table = $"with_comments_{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL,
                 c1 integer,
                 c2 integer,
                 PRIMARY KEY (id)
             );
             COMMENT ON TABLE public.{table} IS 'table comment';
             COMMENT ON COLUMN public.{table}.id IS 'id comment';
             COMMENT ON COLUMN public.{table}.c2 IS 'c2 comment';
             """);
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        
        rs.Move("c1", +1);
        
        await rs.Save(CancellationToken.None);

        Assert.Contains("'table comment'", rs.LastScript);
        Assert.Contains("'id comment'", rs.LastScript);
        Assert.Contains("'c2 comment'", rs.LastScript);
        Assert.DoesNotContain("'c1 comment'", rs.LastScript);
        
        await rs.Load("public", table, CancellationToken.None);

        Assert.NotNull(rs.Table);
        Assert.Equal("table comment", rs.Table.Comments);
        
        Assert.Equal("id comment", rs.FindColumn("id")?.Comments);
        Assert.Equal("c2 comment", rs.FindColumn("c2")?.Comments);
        Assert.Null(rs.FindColumn("c1")?.Comments);
    }

    [Fact]
    public async Task Multiple_Indexes()
    {
        var table = $"multiple_indexes_{TestId}";
        await Db.Raw(
            $"""
             CREATE TABLE public.{table}
             (
                 id integer NOT NULL,
                 c1 integer,
                 c2 varchar,
                 c3 integer,
                 c4 varchar,
                 name varchar,
                 PRIMARY KEY (id)
             );
             CREATE INDEX ON public.{table} USING btree (c1 ASC NULLS LAST) WITH (deduplicate_items=True);
             CREATE INDEX ON public.{table} USING btree (c2 varchar_ops DESC NULLS FIRST) WITH (deduplicate_items=True);
             CREATE UNIQUE INDEX my_index ON public.{table} USING btree (c3 ASC NULLS LAST) NULLS NOT DISTINCT WITH (deduplicate_items=True);
             CREATE INDEX my_expression ON public.{table} USING btree ((lower(c4)) ASC NULLS LAST) WITH (deduplicate_items=True);
             """);
        
        var rs = ReorderService;
        await rs.Load("public", table, CancellationToken.None);
        await rs.Save(CancellationToken.None);

        Assert.Contains("CREATE INDEX multiple_indexes_", rs.LastScript);
        Assert.Contains("CREATE UNIQUE INDEX my_index", rs.LastScript);
        Assert.Contains("CREATE INDEX my_expression", rs.LastScript);
    }
}