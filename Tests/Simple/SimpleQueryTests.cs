
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class SimpleQueryTests
{
    private readonly DbFixture _fx;

    public SimpleQueryTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Distinct_Names_Are_Ordered()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Alpha", Value = 1 };
        new SimpleItem(uow) { Name = "Beta", Value = 2 };
        new SimpleItem(uow) { Name = "Alpha", Value = 3 };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Select(i => i.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();

        names.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void Filter_By_Range_And_Sort_Descending()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Low", Value = 5 };
        new SimpleItem(uow) { Name = "Mid", Value = 10 };
        new SimpleItem(uow) { Name = "High", Value = 15 };
        uow.CommitChanges();

        var results = uow.Query<SimpleItem>()
            .Where(i => i.Value >= 7 && i.Value <= 15)
            .OrderByDescending(i => i.Value)
            .Select(i => new { i.Name, i.Value })
            .ToArray();

        results.Should().HaveCount(2);
        results.Select(r => r.Value).Should().ContainInOrder(15, 10);
    }

    [Fact]
    public void Group_Children_By_Parent_Title()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var p1 = new SimpleParent(uow) { Title = "P1" };
        var p2 = new SimpleParent(uow) { Title = "P2" };
        new SimpleChild(uow) { Label = "A", Order = 1, Parent = p1 };
        new SimpleChild(uow) { Label = "B", Order = 2, Parent = p1 };
        new SimpleChild(uow) { Label = "C", Order = 3, Parent = p2 };
        uow.CommitChanges();

        var grouped = uow.Query<SimpleChild>()
            .Where(c => c.Parent != null)
            .GroupBy(c => c.Parent!.Title)
            .Select(g => new { Title = g.Key, Count = g.Count() })
            .OrderBy(r => r.Title)
            .ToArray();

        grouped.Should().HaveCount(2);
        grouped[0].Title.Should().Be("P1");
        grouped[0].Count.Should().Be(2);
        grouped[1].Title.Should().Be("P2");
        grouped[1].Count.Should().Be(1);
    }

    [Fact]
    public void Deleting_Parent_Cascades_Children()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "Cascade" };
        new SimpleChild(uow) { Label = "X", Order = 1, Parent = parent };
        new SimpleChild(uow) { Label = "Y", Order = 2, Parent = parent };
        uow.CommitChanges();

        parent.Delete();
        uow.CommitChanges();

        uow.Query<SimpleParent>().Count().Should().Be(0);
        uow.Query<SimpleChild>().Count().Should().Be(0);
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

