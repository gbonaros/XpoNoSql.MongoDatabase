
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class WhereAdvancedTests
{
    private readonly DbFixture _fx;

    public WhereAdvancedTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Where_With_And_Or_Groups()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "MatchA", Description = "alpha item", Value = 5, Price = 20m, IsActive = true };
        new SimpleItem(uow) { Name = "MatchB", Description = "beta item", Value = 15, Price = 5m, IsActive = false };
        new SimpleItem(uow) { Name = "Skip", Description = "gamma item", Value = 30, Price = 100m, IsActive = false };
        uow.CommitChanges();

        // (IsActive AND Price < 50) OR (Value BETWEEN 10 AND 20)
        var results = uow.Query<SimpleItem>()
            .Where(i => (i.IsActive && i.Price < 50m) || (i.Value >= 10 && i.Value <= 20))
            .OrderBy(i => i.Value)
            .ToArray();

        results.Should().HaveCount(2);
        results.Select(r => r.Name).Should().Contain(new[] { "MatchA", "MatchB" });
    }

    [Fact]
    public void Where_String_Functions_StartsWith_Contains()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Alpha", Description = "needle-haystack", Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Beta", Description = "random text", Value = 2, Price = 2m, IsActive = true };
        uow.CommitChanges();

        var filtered = uow.Query<SimpleItem>()
            .Where(i => i.Name.StartsWith("Al") && i.Description.Contains("needle"))
            .ToArray();

        filtered.Should().HaveCount(1);
        filtered.Single().Name.Should().Be("Alpha");
    }

    [Fact]
    public void Where_In_List_And_Between()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "One", Description = "test", Value = 5, Price = 10m, IsActive = true };
        new SimpleItem(uow) { Name = "Two", Description = "test", Value = 15, Price = 15m, IsActive = true };
        new SimpleItem(uow) { Name = "Three", Description = "test", Value = 15, Price = 20m, IsActive = true };
        uow.CommitChanges();

        var names = new[] { "One", "Three" };
        var results = uow.Query<SimpleItem>()
            .Where(i => names.Contains(i.Name) && i.Value >= 5 && i.Value <= 20)
            .OrderBy(i => i.Value)
            .ToArray();

        results.Should().HaveCount(2);
        results.Select(r => r.Name).Should().ContainInOrder("One", "Three");
    }

    [Fact]
    public void Where_On_Child_With_Not_And_Category()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "P", Notes = "Parent" };
        new SimpleChild(uow) { Label = "Done", Category = "Keep", Order = 1, IsDone = true, Parent = parent };
        new SimpleChild(uow) { Label = "NotDone", Category = "Keep", Order = 2, IsDone = false, Parent = parent };
        uow.CommitChanges();

        var results = uow.Query<SimpleChild>()
            .Where(c => c.Parent != null && c.Parent.Title == "P" && c.Category == "Keep" && !c.IsDone)
            .ToArray();

        results.Should().HaveCount(1);
        results.Single().Label.Should().Be("NotDone");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

