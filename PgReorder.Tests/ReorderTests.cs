﻿using PgReorder.Core;
using Xunit;

namespace PgReorder.Tests;

public class ReorderTests
{
    [Fact]
    public void Can_Move_Column_Down()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");

        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c1 down from the first spot to the second. This swaps places with c2
        Assert.True(ro.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c1, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c1 down one more spot. It should now be at the bottom on the list.
        Assert.True(ro.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c3, c1]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // We should not be able to move it down anymore.
        Assert.False(ro.Move(c1, +1));
        
        CheckNewOrdinalPosition([c2, c3, c1]);
        CheckOrdinalPosition([c1, c2, c3]);
    }

    [Fact]
    public void Can_Move_Column_Down_Multiple_Offsets()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");

        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c1 down two spots
        Assert.True(ro.Move(c1, +2));
        
        CheckNewOrdinalPosition([c2, c3, c1]);
        CheckOrdinalPosition([c1, c2, c3]);
    }
    
    [Fact]
    public void Can_Move_Selection_Down()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        var c4 = ro.AddColumn("c4");
        var c5 = ro.AddColumn("c5");

        CheckNewOrdinalPosition([c1, c2, c3, c4, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);

        // This should return false because none of the columns are selected
        Assert.False(ro.Move(+1));
        
        c1.IsSelected = true;
        c3.IsSelected = true;
        
        // Move c1 and c3 down one spot
        Assert.True(ro.Move(+1));
        
        CheckNewOrdinalPosition([c2, c1, c4, c3, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Down one more spot
        Assert.True(ro.Move(+1));
        
        CheckNewOrdinalPosition([c2, c4, c1, c5, c3]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Cannot move anymore since c3 reached the bottom of the list
        Assert.False(ro.Move(+1));
    }
    
    [Fact]
    public void Can_Move_Selection_Down_Multiple_Offsets()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        var c4 = ro.AddColumn("c4");
        var c5 = ro.AddColumn("c5");

        CheckNewOrdinalPosition([c1, c2, c3, c4, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);

        // This should return false because none of the columns are selected
        Assert.False(ro.Move(+1));
        
        c1.IsSelected = true;
        c2.IsSelected = true;
        
        Assert.True(ro.Move(+2));
        
        CheckNewOrdinalPosition([c3, c4, c1, c2, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // No longer able to move 2 spots
        Assert.False(ro.Move(+2));
        
        // But, moving it a single spot should be possible
        Assert.True(ro.Move(+1));
        
        CheckNewOrdinalPosition([c3, c4, c5, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Cannot move anymore since c2 reached the bottom of the list
        Assert.False(ro.Move(+1));
    }

    [Fact]
    public void Can_Move_Column_Up()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        
        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c3 up from the last spot to the second. This swaps places with c2
        Assert.True(ro.Move(c3, -1));
        
        CheckNewOrdinalPosition([c1, c3, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c3 up one more spot. It should now be at the top of the list.
        Assert.True(ro.Move(c3, -1));
        
        CheckNewOrdinalPosition([c3, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // We should not able to move it up anymore. The position stays the same.
        Assert.False(ro.Move(c3, -1));
        
        CheckNewOrdinalPosition([c3, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
    }

    [Fact]
    public void Can_Move_Column_Up_Multiple_Offsets()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");

        CheckNewOrdinalPosition([c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3]);
        
        // Move c3 down two spots
        Assert.True(ro.Move(c3, -2));
        
        CheckNewOrdinalPosition([c3, c1, c2]);
        CheckOrdinalPosition([c1, c2, c3]);
    }
    
    [Fact]
    public void Can_Move_Selection_Up()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        var c4 = ro.AddColumn("c4");
        var c5 = ro.AddColumn("c5");

        CheckNewOrdinalPosition([c1, c2, c3, c4, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);

        // This should return false because none of the columns are selected
        Assert.False(ro.Move(-1));
        
        c3.IsSelected = true;
        c5.IsSelected = true;
        
        // Move c5 and c3 up one spot
        Assert.True(ro.Move(-1));
        
        CheckNewOrdinalPosition([c1, c3, c2, c5, c4]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Up one more spot
        Assert.True(ro.Move(-1));
        
        CheckNewOrdinalPosition([c3, c1, c5, c2, c4]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Cannot move anymore since c3 reached the bottom of the list
        Assert.False(ro.Move(-1));
    }
    
    [Fact]
    public void Can_Move_Selection_Up_Multiple_Offsets()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        var c4 = ro.AddColumn("c4");
        var c5 = ro.AddColumn("c5");

        CheckNewOrdinalPosition([c1, c2, c3, c4, c5]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);

        // This should return false because none of the columns are selected
        Assert.False(ro.Move(-1));
        
        c4.IsSelected = true;
        c5.IsSelected = true;
        
        Assert.True(ro.Move(-2));
        
        CheckNewOrdinalPosition([c1, c4, c5, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // No longer able to move 2 spots
        Assert.False(ro.Move(-2));
        
        // But, moving it a single spot should be possible
        Assert.True(ro.Move(-1));
        
        CheckNewOrdinalPosition([c4, c5, c1, c2, c3]);
        CheckOrdinalPosition([c1, c2, c3, c4, c5]);
        
        // Cannot move anymore since c4 reached the bottom of the list
        Assert.False(ro.Move(-1));
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
        var ro = new Reorder();

        var c1 = ro.AddColumn("c1");
        var c2 = ro.AddColumn("c2");
        var c3 = ro.AddColumn("c3");
        
        // Try to move the column. The order after the move should not be affected since it would be out of bounds.
        Assert.False(ro.Move(columnName, position));
        
        Assert.Equal(1, c1.NewOrdinalPosition);
        Assert.Equal(2, c2.NewOrdinalPosition);
        Assert.Equal(3, c3.NewOrdinalPosition);
    }

    [Fact]
    public void Cannot_Move_Non_Existent_Column()
    {
        var ro = new Reorder();

        var exception = Assert.Throws<Exception>(() => ro.Move("not_found", 1));

        Assert.Contains("Could not find column", exception.Message);
    }

    [Fact]
    public void Can_Sort_Columns_In_Alphabetical_Order()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("c");
        var c2 = ro.AddColumn("B");
        var c3 = ro.AddColumn("a");
        
        ro.SortInAlphabeticalOrder();
        
        Assert.Equal(1, c3.NewOrdinalPosition);
        Assert.Equal(2, c2.NewOrdinalPosition);
        Assert.Equal(3, c1.NewOrdinalPosition);
    }
    
    [Fact]
    public void Can_Sort_Columns_In_Reverse_Alphabetical_Order()
    {
        var ro = new Reorder();

        var c1 = ro.AddColumn("a");
        var c2 = ro.AddColumn("B");
        var c3 = ro.AddColumn("c");
        
        ro.SortInReverseAlphabeticalOrder();
        
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