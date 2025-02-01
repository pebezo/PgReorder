using PgReorder.Core;
using Xunit;

namespace PgReorder.Tests;

public class PgTableTests
{
    [Fact]
    public void Can_Move_Column_Down()
    {
        var table = new PgTable("schema1", "table1");

        var c1 = table.AddColumn("c1");
        var c2 = table.AddColumn("c2");
        var c3 = table.AddColumn("c3");

        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c1 down from the first spot to the second. This swaps places with c2
        Assert.True(table.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c1, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c1 down one more spot. It should now be at the bottom on the list.
        Assert.True(table.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c3, c1]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // We should not be able to move it down anymore.
        Assert.False(table.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c3, c1]);
        CheckOrdinalPosition([c1, c2, c3]);
    }

    [Fact]
    public void Can_Move_Column_Up()
    {
        var table = new PgTable("schema1", "table1");

        var c1 = table.AddColumn("c1");
        var c2 = table.AddColumn("c2");
        var c3 = table.AddColumn("c3");
        
        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c3 up from the last spot to the second. This swaps places with c2
        Assert.True(table.Move(c3, -1));
        
        CheckNewOrdinalPosition([c1, c3, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c3 up one more spot. It should now be at the top of the list.
        Assert.True(table.Move(c3, -1));
        
        CheckNewOrdinalPosition([c3, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // We should not able to move it up anymore. The position stays the same.
        Assert.False(table.Move(c3, -1));
        
        CheckNewOrdinalPosition([c3, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
    }

    [Theory]
    [InlineData("c1", -1)]
    [InlineData("c1", -10)]
    [InlineData("c1", 3)]
    [InlineData("c1", 30)]
    [InlineData("c2", -2)]
    [InlineData("c2", -20)]
    [InlineData("c2", 2)]
    [InlineData("c2", 20)]
    [InlineData("c3", -3)]
    [InlineData("c3", -30)]
    [InlineData("c3", 1)]
    [InlineData("c3", 10)]
    public void Cannot_Move_Column_Outside_Of_Bounds(string columnName, int position)
    {
        var table = new PgTable("schema1", "table1");

        var c1 = table.AddColumn("c1");
        var c2 = table.AddColumn("c2");
        var c3 = table.AddColumn("c3");
        
        // Try to move the column. The order after the move should not be affected since it would be out of bounds.
        Assert.False(table.Move(columnName, position));
        
        Assert.Equal(1, c1.NewOrdinalPosition);
        Assert.Equal(2, c2.NewOrdinalPosition);
        Assert.Equal(3, c3.NewOrdinalPosition);
    }

    [Fact]
    public void Cannot_Move_Non_Existent_Column()
    {
        var table = new PgTable("schema1", "table1");

        var exception = Assert.Throws<Exception>(() => table.Move("not_found", 1));

        Assert.Contains("Could not find column", exception.Message);
    }

    [Fact]
    public void Can_Sort_Columns_In_Alphabetical_Order()
    {
        var table = new PgTable("schema1", "table1");

        var c1 = table.AddColumn("c");
        var c2 = table.AddColumn("B");
        var c3 = table.AddColumn("a");
        
        table.SortInAlphabeticalOrder();
        
        Assert.Equal(1, c3.NewOrdinalPosition);
        Assert.Equal(2, c2.NewOrdinalPosition);
        Assert.Equal(3, c1.NewOrdinalPosition);
    }
    
    [Fact]
    public void Can_Sort_Columns_In_Reverse_Alphabetical_Order()
    {
        var table = new PgTable("schema1", "table1");

        var c1 = table.AddColumn("a");
        var c2 = table.AddColumn("B");
        var c3 = table.AddColumn("c");
        
        table.SortInReverseAlphabeticalOrder();
        
        Assert.Equal(1, c3.NewOrdinalPosition);
        Assert.Equal(2, c2.NewOrdinalPosition);
        Assert.Equal(3, c1.NewOrdinalPosition);
    }
    
    private static void CheckNewOrdinalPosition(IEnumerable<PgColumn> columns)
    {
        var index = 0;
        foreach (var column in columns)
        {
            index++;
            Assert.Equal(index, column.NewOrdinalPosition);
        }
    }
    
    private static void CheckOrdinalPosition(IEnumerable<PgColumn> columns)
    {
        var index = 0;
        foreach (var column in columns)
        {
            index++;
            Assert.Equal(index, column.OrdinalPosition);
        }
    }
}