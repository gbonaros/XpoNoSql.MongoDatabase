
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class CriteriaOperatorTests
{
    private readonly DbFixture _fx;

    public CriteriaOperatorTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void BetweenOperator_Filters_Range()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Low", Price = 5m, Value = 1, IsActive = true, Description = "L" };
        new SimpleItem(uow) { Name = "Mid", Price = 10m, Value = 2, IsActive = true, Description = "M" };
        new SimpleItem(uow) { Name = "High", Price = 20m, Value = 3, IsActive = true, Description = "H" };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => i.Price >= 8m && i.Price <= 15m) // BETWEEN equivalent
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("Mid");
    }

    [Fact]
    public void BetweenOperator_On_DateRange()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var start = new DateTime(2024, 1, 1);
        var mid = new DateTime(2024, 6, 1);
        var end = new DateTime(2024, 12, 31);

        new SimpleItem(uow) { Name = "Past", CreatedOn = start.AddDays(-1), Price = 1m, Value = 1, IsActive = true, Description = "P" };
        new SimpleItem(uow) { Name = "Mid", CreatedOn = mid, Price = 1m, Value = 1, IsActive = true, Description = "M" };
        new SimpleItem(uow) { Name = "Future", CreatedOn = end.AddDays(1), Price = 1m, Value = 1, IsActive = true, Description = "F" };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => i.CreatedOn >= start && i.CreatedOn <= end)
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("Mid");
    }

    [Fact]
    public void InOperator_Filters_By_List()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "A", Price = 1m, Value = 1, IsActive = true, Description = "A" };
        new SimpleItem(uow) { Name = "B", Price = 1m, Value = 1, IsActive = true, Description = "B" };
        new SimpleItem(uow) { Name = "C", Price = 1m, Value = 1, IsActive = true, Description = "C" };
        uow.CommitChanges();

        var allowed = new[] { "A", "C" };
        var names = uow.Query<SimpleItem>()
            .Where(i => allowed.Contains(i.Name))
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("A", "C");
    }

    [Fact]
    public void InOperator_With_Subquery_Projection()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "P", Notes = "Parent" };
        new SimpleChild(uow) { Label = "Keep", Category = "Cat1", Order = 1, IsDone = true, Parent = parent };
        new SimpleChild(uow) { Label = "Skip", Category = "Cat2", Order = 2, IsDone = true, Parent = parent };
        uow.CommitChanges();

        // IN against subquery projection (child categories)
        var categories = uow.Query<SimpleChild>()
            .Select(c => c.Category)
            .Distinct()
            .ToArray();

        var parents = uow.Query<SimpleParent>()
            .Where(p => categories.Contains("Cat1"))
            .ToArray();

        parents.Should().HaveCount(1);
        parents.Single().Title.Should().Be("P");
    }

    [Fact]
    public void GroupOperator_Nested_And_Or()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Match1", Description = "foo", Price = 10m, Value = 1, IsActive = true };
        new SimpleItem(uow) { Name = "Match2", Description = "bar", Price = 15m, Value = 2, IsActive = false };
        new SimpleItem(uow) { Name = "Skip", Description = "baz", Price = 30m, Value = 3, IsActive = true };
        uow.CommitChanges();

        // (IsActive AND Price < 20) OR (!IsActive AND Description == 'bar')
        var names = uow.Query<SimpleItem>()
            .Where(i => (i.IsActive && i.Price < 20m) || (!i.IsActive && i.Description == "bar"))
            .Select(i => i.Name)
            .OrderBy(n => n)
            .ToArray();

        names.Should().Equal("Match1", "Match2");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

