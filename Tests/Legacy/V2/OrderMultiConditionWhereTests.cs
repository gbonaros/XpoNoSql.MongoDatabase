
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OrderMultiConditionWhereTests
{
    private readonly DbFixture _fx;
    public OrderMultiConditionWhereTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Filter_Orders_By_Total_Range_And_ProductName()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var c = new TestCustomer(uow) { Name = "Customer" };

            new TestOrder(uow) { Customer = c, ProductName = "Pencil", Quantity = 1, Total = 5 };
            new TestOrder(uow) { Customer = c, ProductName = "Pen", Quantity = 2, Total = 10 };
            new TestOrder(uow) { Customer = c, ProductName = "Notebook", Quantity = 3, Total = 20 };
            new TestOrder(uow) { Customer = c, ProductName = "Marker", Quantity = 4, Total = 30 };
            uow.CommitChanges();

            decimal min = 10;
            decimal max = 25;

            var result = uow.Query<TestOrder>()
                .Where(o =>
                    o.Total >= min &&
                    o.Total <= max &&
                    (o.ProductName == "Pen" || o.ProductName == "Notebook"))
                .OrderBy(o => o.Total)
                .ToArray();

            result.Should().HaveCount(2);
            result[0].ProductName.Should().Be("Pen");
            result[1].ProductName.Should().Be("Notebook");
        }
    }

    [Fact]
    public void Filter_Orders_By_CustomerName_And_Quantity_Or_Total()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var a = new TestCustomer(uow) { Name = "Alice" };
            var b = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = a, ProductName = "A1", Quantity = 1, Total = 10 };
            new TestOrder(uow) { Customer = a, ProductName = "A2", Quantity = 5, Total = 15 };
            new TestOrder(uow) { Customer = b, ProductName = "B1", Quantity = 10, Total = 5 };
            new TestOrder(uow) { Customer = b, ProductName = "B2", Quantity = 2, Total = 50 };
            uow.CommitChanges();

            // WHERE (Customer.Name = 'Alice' AND Quantity >= 5) OR (Total >= 40)
            var result = uow.Query<TestOrder>()
                .Where(o =>
                    (o.Customer != null && o.Customer.Name == "Alice" && o.Quantity >= 5) ||
                    o.Total >= 40)
                .OrderBy(o => o.Customer!.Name)
                .ThenBy(o => o.ProductName)
                .ToArray();

            result.Should().HaveCount(2);
            result[0].Customer!.Name.Should().Be("Alice");
            result[0].ProductName.Should().Be("A2");
            result[1].Customer!.Name.Should().Be("Bob");
            result[1].ProductName.Should().Be("B2");
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var o in new XPCollection<TestOrder>(uow).ToList()) o.Delete();
        foreach (var c in new XPCollection<TestCustomer>(uow).ToList()) c.Delete();
        uow.CommitChanges();
    }
}
