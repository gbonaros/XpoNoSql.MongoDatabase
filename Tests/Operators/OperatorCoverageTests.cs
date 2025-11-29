
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OperatorCoverageTests
{
    private readonly DbFixture _fx;

    public OperatorCoverageTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void ContainsOperator_Equivalent_String_Contains()
    {
        CleanupSimple();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Hit", Description = "needle in haystack", Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Miss", Description = "other text", Value = 1, Price = 1m, IsActive = true };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => i.Description.Contains("needle"))
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("Hit");
    }

    [Fact]
    public void AggregateOperand_Equivalent_Group_By_Sum()
    {
        CleanupCustomers();
        using var uow = _fx.NewUow();

        var alice = new TestCustomer(uow) { Name = "Alice" };
        var bob = new TestCustomer(uow) { Name = "Bob" };

        new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 50m };
        new TestOrder(uow) { Customer = alice, ProductName = "A2", Quantity = 1, Total = 60m };
        new TestOrder(uow) { Customer = bob, ProductName = "B1", Quantity = 1, Total = 20m };
        uow.CommitChanges();

        var result = uow.Query<TestOrder>()
            .GroupBy(o => o.Customer!.Name)
            .Select(g => new { Customer = g.Key, SumTotal = g.Sum(o => o.Total) })
            .Where(r => r.SumTotal >= 100m)
            .ToArray();

        result.Should().HaveCount(1);
        result[0].Customer.Should().Be("Alice");
        result[0].SumTotal.Should().Be(110m);
    }

    [Fact]
    public void JoinOperand_Equivalent_Navigation_Filter()
    {
        CleanupCustomers();
        using var uow = _fx.NewUow();

        var alice = new TestCustomer(uow) { Name = "Alice" };
        var bob = new TestCustomer(uow) { Name = "Bob" };

        new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 10m };
        new TestOrder(uow) { Customer = bob, ProductName = "B1", Quantity = 1, Total = 10m };
        uow.CommitChanges();

        var names = uow.Query<TestOrder>()
            .Where(o => o.Customer != null && o.Customer.Name == "Alice")
            .Select(o => o.ProductName)
            .ToArray();

        names.Should().Equal("A1");
    }

    [Fact]
    public void NotOperator_Negates_Condition()
    {
        CleanupSimple();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Cheap", Price = 5m, Value = 1, IsActive = true, Description = "C" };
        new SimpleItem(uow) { Name = "Expensive", Price = 50m, Value = 1, IsActive = true, Description = "E" };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => !(i.Price > 10m))
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("Cheap");
    }

    [Fact]
    public void NullOperator_Checks_For_Null()
    {
        CleanupSimple();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "NullDesc", Description = null!, Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "HasDesc", Description = "text", Value = 1, Price = 1m, IsActive = true };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => i.Description == null)
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("NullDesc");
    }

    private void CleanupSimple()
    {
        _fx.CleanupAll();
    }

    private void CleanupCustomers()
    {
        _fx.CleanupAll();
    }
}

