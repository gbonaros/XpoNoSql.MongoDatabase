
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class CustomerDistinctTests
{
    private readonly DbFixture _fx;
    public CustomerDistinctTests(DbFixture fx) => _fx = fx;

    //[Fact]
    //public void Skip1Take1()
    //{
    //    using (var uow = _fx.NewUow())
    //    {
    //        Cleanup(uow);
    //        for (int i = 0; i < 10; i++)
    //        {
    //            var c = new TestCustomer(uow) { Name = $"Alice {i}" };

    //        }
    //        uow.CommitChanges();

    //        var names = uow.Query<TestCustomer>()
    //            .Skip(1)
    //            .TakeWhile(t=>t.Name == "Alice 3")
    //            .Skip(2)
    //            .TakeWhile(t => t.Name == "Alice 8")
    //            .Select(o => o.Name)
    //            .ToArray();

    //        names.Should().Equal("Alice 1");
    //    }

    //}
    [Fact]
    public void Distinct_CustomerNames_With_Orders()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();
        using (var uow = _fx.NewUow())
        {

            var a1 = new TestCustomer(uow) { Name = "Alice" };
            var a2 = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };
            var c = new TestCustomer(uow) { Name = "Charlie" };

            new TestOrder(uow) { Customer = a1, Total = 10 };
            new TestOrder(uow) { Customer = b, Total = 20 };
            // Charlie has no orders
            uow.CommitChanges();

            var names = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .Select(o => o.Customer!.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            names.Should().Equal("Alice", "Bob");
        }
    }

    [Fact]
    public void Distinct_Customers_By_Name_From_Orders()
    {
        _fx.Cleanup<TestCustomer>();
        _fx.Cleanup<TestOrder>();
        using (var uow = _fx.NewUow())
        {
            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = a, ProductName = "P1", Total = 5 };
            new TestOrder(uow) { Customer = a, ProductName = "P2", Total = 10 };
            new TestOrder(uow) { Customer = b, ProductName = "P3", Total = 15 };
            uow.CommitChanges();

            var customers = uow.Query<TestOrder>()
                .Where(o => o.Customer != null)
                .Select(o => new { Cust = o.Customer, o.Customer!.Name })
                .Distinct()
                .OrderBy(c => c.Name)
                .Select(c => c.Cust)
                .ToArray();

            customers.Should().HaveCount(2);
            customers[0]!.Name.Should().Be("Alice");
            customers[1]!.Name.Should().Be("Bob");
        }
    }
}
