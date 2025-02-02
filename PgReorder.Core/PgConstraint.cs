namespace PgReorder.Core;

/// <summary>
/// Postgres constraint that maps to:
/// https://www.postgresql.org/docs/current/catalog-pg-constraint.html
/// </summary>
public class PgConstraint
{
    /// <summary>
    /// Constraint name (not necessarily unique!)
    /// </summary>
    public required string? Name { get; set; }
    
    /// <summary>
    /// Constraint type:
    ///     c = check constraint,
    ///     f = foreign key constraint,
    ///     n = not-null constraint (domains only),
    ///     p = primary key constraint,
    ///     u = unique constraint,
    ///     t = constraint trigger,
    ///     x = exclusion constraint
    /// </summary>
    public required char? Type { get; set; }
    
    /// <summary>
    /// DDL definition of this constraint, for example:
    ///     PRIMARY KEY (id)
    ///     FOREIGN KEY (col) REFERENCES table2(col) ON UPDATE CASCADE
    /// </summary>
    public required string? Definition { get; set; }

    /// <summary>
    /// Treat foreign-key constraints differently from all the other constraints
    /// </summary>
    public bool UseInCreateTable => Type is not 'f';
}