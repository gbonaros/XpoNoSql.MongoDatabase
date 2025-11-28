
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.OrderBy;

[Collection(XpoCollection.Name)]
public class OrderBasicTests
{
    private readonly DbFixture _fx;

    public OrderBasicTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Order_Items_By_Value_Ascending()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "C", Value = 30, Price = 3m, IsActive = true };
        new SimpleItem(uow) { Name = "A", Value = 10, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "B", Value = 20, Price = 2m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .OrderBy(i => i.Value)
            .Select(i => i.Name)
            .ToArray();

        ordered.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Order_By_Price_Descending_Then_Name()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "X", Value = 1, Price = 9m, IsActive = true };
        new SimpleItem(uow) { Name = "Y", Value = 2, Price = 9m, IsActive = true };
        new SimpleItem(uow) { Name = "Z", Value = 3, Price = 5m, IsActive = true };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .OrderByDescending(i => i.Price)
            .ThenBy(i => i.Name)
            .Select(i => i.Name)
            .ToArray();

        ordered.Should().Equal("X", "Y", "Z");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

