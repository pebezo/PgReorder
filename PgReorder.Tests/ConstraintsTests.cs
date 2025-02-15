using Xunit;

namespace PgReorder.Tests;

public class ConstraintsTests(DockerFixture fixture) : DockerBase(fixture)
{
    [Fact]
    public async Task Single_Foreign_Key_Dependency()
    {
        var table = "single_foreign_key";
        var fk1 = "single_foreign_key_fk1";
        await Db.Raw($"""
                     CREATE TABLE public.{fk1}
                     (
                         id integer NOT NULL PRIMARY KEY,
                         name varchar NOT NULL
                     );
                     INSERT INTO public.{fk1} (id, name) VALUES (10, 'Cat1');
                     INSERT INTO public.{fk1} (id, name) VALUES (20, 'Cat2');
                     CREATE TABLE public.{table}
                     (
                         id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                         name varchar NOT NULL,
                         fk1_id integer NOT NULL
                     );
                     ALTER TABLE public.{table}
                        ADD CONSTRAINT original_{table}_{fk1}_id_fk
                            FOREIGN KEY (fk1_id) REFERENCES {fk1};
                            
                     INSERT INTO public.{table} (name, fk1_id) VALUES ('P1', 10);
                     INSERT INTO public.{table} (name, fk1_id) VALUES ('P2', 20);
                     INSERT INTO public.{table} (name, fk1_id) VALUES ('P3', 10);
                     """);
        
        var rts = ReorderTableService;
        await rts.Load("public", table, CancellationToken.None);
        
        rts.Move(rts.LoadedColumns.GetColumn("fk1_id"), -1);
        
        await rts.Save(CancellationToken.None);
        
        var reloaded = await CheckColumnDefinition(rts.LoadedColumns);

        // Make sure the run ID did not leave traces in the foreign key name and that the 'original' name carried over
        Assert.NotNull(rts.LastRunId);
        Assert.Contains(reloaded.AllForeignKeyConstraints(), p => p.Name is not null && p.Name.Contains("original"));
        Assert.DoesNotContain(reloaded.AllForeignKeyConstraints(), p => p.Name is not null && p.Name.Contains(rts.LastRunId));
        
        // Add a new row after reordering
        await Db.Raw($"INSERT INTO public.{table} (name, fk1_id) VALUES ('P4', 20)");
        
        // Expecting: id = 4, category_id = 20, name = P4 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 4, 20, "P4");
    }
    
    [Fact]
    public async Task Multiple_Foreign_Keys_Dependency()
    {
        var table = "multiple_foreign_keys";
        var fk1 = "multiple_foreign_keys_fk1";
        var fk2 = "multiple_foreign_keys_fk2";
        await Db.Raw($"""
                      CREATE TABLE public.{fk1}
                      (
                          id integer NOT NULL PRIMARY KEY,
                          name varchar NOT NULL
                      );
                      INSERT INTO public.{fk1} (id, name) VALUES (10, 'Cat1');
                      INSERT INTO public.{fk1} (id, name) VALUES (20, 'Cat2');
                      CREATE TABLE public.{fk2}
                      (
                          id integer NOT NULL PRIMARY KEY,
                          name varchar NOT NULL
                      );
                      INSERT INTO public.{fk2} (id, name) VALUES (90, 'Cat3');
                      CREATE TABLE public.{table}
                      (
                          id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                          name varchar NOT NULL,
                          fk1_id integer NOT NULL,
                          fk2_id integer NOT NULL
                      );
                      ALTER TABLE public.{table}
                         ADD CONSTRAINT original_{table}_{fk1}_id_fk
                             FOREIGN KEY (fk1_id) REFERENCES {fk1};
                      ALTER TABLE public.{table}
                         ADD CONSTRAINT original_{table}_{fk2}_id_fk
                            FOREIGN KEY (fk2_id) REFERENCES {fk2};
                             
                      INSERT INTO public.{table} (name, fk1_id, fk2_id) VALUES ('P1', 10, 90);
                      INSERT INTO public.{table} (name, fk1_id, fk2_id) VALUES ('P2', 20, 90);
                      INSERT INTO public.{table} (name, fk1_id, fk2_id) VALUES ('P3', 10, 90);
                      """);
        
        var rts = ReorderTableService;
        await rts.Load("public", table, CancellationToken.None);
        
        rts.Move(rts.LoadedColumns.GetColumn("name"), +2);
        
        await rts.Save(CancellationToken.None);
        
        var reloaded = await CheckColumnDefinition(rts.LoadedColumns);
        
        // Make sure the run ID did not leave traces in the foreign key name and that the 'original' name carried over
        Assert.NotNull(rts.LastRunId);
        Assert.Contains(reloaded.AllForeignKeyConstraints(), p => p.Name is not null && p.Name.Contains("original"));
        Assert.DoesNotContain(reloaded.AllForeignKeyConstraints(), p => p.Name is not null && p.Name.Contains(rts.LastRunId));
        
        // Add a new row after reordering
        await Db.Raw($"INSERT INTO public.{table} (name, fk1_id, fk2_id) VALUES ('P4', 20, 90)");
        
        // Expecting: id = 4, category_id = 20, name = P4 
        await CheckRowValues($"SELECT * FROM public.{table} ORDER BY id DESC LIMIT 1", table, 4, 20, 90, "P4");
        
    }
}