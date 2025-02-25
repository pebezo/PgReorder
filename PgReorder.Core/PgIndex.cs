namespace PgReorder.Core;

public class PgIndex
{
    /// <summary>
    /// The name of the index
    /// </summary>
    public required string? Name { get; set; }
    
    /// <summary>
    /// DDL definition of this index, for example:
    ///     CREATE INDEX my_table_idx ON public.my_table USING btree (id) WITH (deduplicate_items='true')
    /// </summary>
    public required string? Definition { get; set; }
}