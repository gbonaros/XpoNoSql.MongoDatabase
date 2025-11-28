
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Extreme;

[Collection(XpoCollection.Name)]
public class ExtremeOrderingTests
{
    private readonly DbFixture _fx;

    public ExtremeOrderingTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Order_By_Multiple_Fields_With_Ties()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Tie1", Description = "A", Value = 10, Price = 50m, IsActive = true };
        new SimpleItem(uow) { Name = "Tie2", Description = "B", Value = 10, Price = 50m, IsActive = true };
        new SimpleItem(uow) { Name = "High", Description = "C", Value = 20, Price = 100m, IsActive = true };
        new SimpleItem(uow) { Name = "Low", Description = "D", Value = 5, Price = 10m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .OrderByDescending(i => i.Price)
            .ThenByDescending(i => i.Value)
            .ThenBy(i => i.Name)
            .Select(i => i.Name)
            .ToArray();

        ordered.Should().Equal("High", "Tie1", "Tie2", "Low");
    }

    [Fact]
    public void Order_With_Computed_Length_And_Reverse()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Alpha", Description = "short", Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Beta", Description = "much longer", Value = 2, Price = 2m, IsActive = true };
        new SimpleItem(uow) { Name = "Gamma", Description = "medium", Value = 3, Price = 3m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .Select(i => new { i.Name, Len = i.Description.Length })
            .OrderByDescending(i => i.Len)
            .ThenByDescending(i => i.Name)
            .ToArray();

        ordered[0].Name.Should().Be("Beta");
        ordered[1].Name.Should().Be("Gamma");
        ordered[2].Name.Should().Be("Alpha");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

