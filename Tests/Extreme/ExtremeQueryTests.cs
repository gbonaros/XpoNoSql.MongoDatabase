
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Extreme;

[Collection(XpoCollection.Name)]
public class ExtremeQueryTests
{
    private readonly DbFixture _fx;

    public ExtremeQueryTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Deeply_Nested_Logical_Expressions()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Hit", Description = "match me", Value = 10, Price = 15m, IsActive = true };
        new SimpleItem(uow) { Name = "Miss1", Description = "match me", Value = 1, Price = 1m, IsActive = false };
        new SimpleItem(uow) { Name = "Miss2", Description = "ignore", Value = 20, Price = 25m, IsActive = true };
        uow.CommitChanges();

        var result = uow.Query<SimpleItem>()
            .Where(i =>
                (i.IsActive && i.Description.Contains("match") && i.Value > 5) ||
                (!i.IsActive && i.Price > 100m) ||
                (i.Value >= 10 && i.Value <= 15 && i.Price < 20m))
            .Select(i => i.Name)
            .ToArray();

        result.Should().ContainSingle("Hit");
    }

    [Fact]
    public void Ordering_On_Multiple_Computed_Fields()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "First", Description = "abc", Value = 1, Price = 10m, IsActive = true };
        new SimpleItem(uow) { Name = "Second", Description = "abcd", Value = 2, Price = 5m, IsActive = true };
        new SimpleItem(uow) { Name = "Third", Description = "ab", Value = 3, Price = 10m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .Select(i => new
            {
                i.Name,
                Len = i.Description.Length,
                Total = i.Price * i.Value
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Len)
            .ToArray();

        ordered[0].Name.Should().Be("Third"); // 10 * 3 = 30
        ordered[1].Name.Should().Be("First"); // 10 * 1 = 10 len 3
        ordered[2].Name.Should().Be("Second"); // 5 * 2 = 10 len 4
    }

    [Fact]
    public void Cascade_Delete_Check_In_Extreme_Setup()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "CascadeExtreme", Notes = "Extreme" };
        for (int i = 0; i < 5; i++)
        {
            new SimpleChild(uow) { Label = $"Child{i}", Category = i % 2 == 0 ? "Even" : "Odd", Order = i, IsDone = i % 2 == 0, Parent = parent };
        }
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

