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
    protected ReorderTableService ReorderTableService => _fixture.Build<ReorderTableService>();
}