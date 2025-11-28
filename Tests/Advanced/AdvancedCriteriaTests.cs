
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Advanced;

[Collection(XpoCollection.Name)]
public class AdvancedCriteriaTests
{
    private readonly DbFixture _fx;

    public AdvancedCriteriaTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Complex_And_Or_Not_Combination()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Target1", Description = "abc xyz", Value = 5, Price = 10m, IsActive = true };
        new SimpleItem(uow) { Name = "Target2", Description = "abc nope", Value = 15, Price = 20m, IsActive = false };
        new SimpleItem(uow) { Name = "Skip", Description = "zzz nope", Value = 25, Price = 30m, IsActive = false };
        uow.CommitChanges();

        // (IsActive AND Description contains 'abc') OR (NOT IsActive AND Value BETWEEN 10 AND 20)
        var results = uow.Query<SimpleItem>()
            .Where(i => (i.IsActive && i.Description.Contains("abc")) || (!i.IsActive && i.Value >= 10 && i.Value <= 20))
            .OrderBy(i => i.Value)
            .Select(i => i.Name)
            .ToArray();

        results.Should().Equal("Target1", "Target2");
    }

    [Fact]
    public void Function_Based_Filtering_And_Sorting()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Upper", Description = "HELLO WORLD", Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Lower", Description = "hello world", Value = 2, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Mixed", Description = "HeLlO WoRlD", Value = 3, Price = 1m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .Where(i => i.Description.ToLower().StartsWith("hello"))
            .OrderBy(i => i.Description.ToUpper())
            .Select(i => i.Name)
            .ToArray();

        ordered.Should().Equal("Upper", "Lower", "Mixed");
    }

    [Fact]
    public void Group_By_Category_With_Having_On_Count()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var p = new SimpleParent(uow) { Title = "Grouping", Notes = "G" };
        new SimpleChild(uow) { Label = "A", Category = "Cat1", Order = 1, IsDone = true, Parent = p };
        new SimpleChild(uow) { Label = "B", Category = "Cat1", Order = 2, IsDone = true, Parent = p };
        new SimpleChild(uow) { Label = "C", Category = "Cat2", Order = 3, IsDone = true, Parent = p };
        uow.CommitChanges();

        var grouped = uow.Query<SimpleChild>()
            .Where(c => c.Parent != null)
            .GroupBy(c => c.Category)
            .Where(g => g.Count() >= 2)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToArray();

        grouped.Should().HaveCount(1);
        grouped.Single().Category.Should().Be("Cat1");
        grouped.Single().Count.Should().Be(2);
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

