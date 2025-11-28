
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Combinations;

[Collection(XpoCollection.Name)]
public class CombinationQueryTests
{
    private readonly DbFixture _fx;

    public CombinationQueryTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Filter_Group_Order_MultiStep()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "WidgetA", Description = "hardware", Value = 1, Price = 10m, IsActive = true };
        new SimpleItem(uow) { Name = "WidgetB", Description = "hardware", Value = 2, Price = 15m, IsActive = true };
        new SimpleItem(uow) { Name = "Service", Description = "software", Value = 3, Price = 5m, IsActive = false };
        uow.CommitChanges();

        var grouped = uow.Query<SimpleItem>()
            .Where(i => i.Price >= 10m && i.IsActive)
            .GroupBy(i => i.Description)
            .Select(g => new { Desc = g.Key, Count = g.Count(), Total = g.Sum(i => i.Price) })
            .OrderByDescending(r => r.Total)
            .ToArray();

        grouped.Should().HaveCount(1);
        grouped[0].Desc.Should().Be("hardware");
        grouped[0].Count.Should().Be(2);
        grouped[0].Total.Should().Be(25m);
    }

    [Fact]
    public void Order_With_Where_And_Projection_Shaping()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Alpha", Description = "A", Value = 5, Price = 30m, IsActive = true };
        new SimpleItem(uow) { Name = "Beta", Description = "B", Value = 10, Price = 20m, IsActive = true };
        new SimpleItem(uow) { Name = "Gamma", Description = "C", Value = 15, Price = 25m, IsActive = false };
        uow.CommitChanges();

        var shaped = uow.Query<SimpleItem>()
            .Where(i => i.IsActive && i.Price >= 20m)
            .Select(i => new { Key = i.Name.Substring(0, 1), i.Price, Score = i.Price + i.Value })
            .OrderByDescending(i => i.Score)
            .ThenBy(i => i.Key)
            .ToArray();

        shaped.Should().HaveCount(2);
        shaped[0].Key.Should().Be("A");
        shaped[1].Key.Should().Be("B");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

