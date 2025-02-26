using System.Data;
using PgReorder.Core;
using Xunit;

namespace PgReorder.Tests;

[Collection("Docker")]
public abstract class DockerBase
{
    protected DockerBase(DockerFixture fixture)
    {
        _fixture = fixture;
        TestId++;
    }

    private readonly DockerFixture _fixture;
    protected static int TestId { get; private set; }

    protected DatabaseRepository Db => _fixture.Build<DatabaseRepository>();
    protected ReorderService ReorderService => _fixture.Build<ReorderService>();
    
    /// <summary>
    /// Reload a table from the database and makes sure the column definition / type for each column has not changed
    /// </summary>
    protected async Task<ReorderService> CheckColumnDefinition(ReorderService source)
    {
        var target = ReorderService;
        await target.Load(source.Schema?.SchemaName, source.Table?.TableName, CancellationToken.None);
        source.Compare(target);
        return target;
    }

    /// <summary>
    /// Given a SQL statement, we look at the first row and check the order and column values 
    /// </summary>
    protected async Task CheckRowValues(string sql, string table, params IList<object> expected)
    {
        var dt = await Db.RawDataTable(sql);

        Assert.NotNull(dt);
        Assert.NotEmpty(dt.Rows);

        for (int i = 0; i < expected.Count; i++)
        {
            var actual = dt.Rows[0].Field<object>(i);
            if (actual is null || !actual.Equals(expected[i]))
            {
                Assert.Fail($"Table '{table}' column #{i} has '{actual}', expecting '{expected[i]}'");    
            }
        }
    }
}