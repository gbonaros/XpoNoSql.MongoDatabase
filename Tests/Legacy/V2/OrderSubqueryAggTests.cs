using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OrderSubqueryAggTests
{
    private readonly DbFixture _fx;
    public OrderSubqueryAggTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Customer_With_At_Least_2_Orders()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var a = new TestCustomer(uow) { Name = "A" };
            var b = new TestCustomer(uow) { Name = "B" };

            new TestOrder(uow) { Customer = a, Total = 10 };
            new TestOrder(uow) { Customer = a, Total = 20 };
            new TestOrder(uow) { Customer = b, Total = 30 };
            uow.CommitChanges();

            // SELECT * FROM Customer WHERE (SELECT COUNT(*) FROM Orders o WHERE o.Customer=c) >= 2
            var result = uow.Query<TestCustomer>()
                .Where(c => c.Orders.Count() >= 2)
                .Select(c => new
                {
                    c.Name,
                    TotalOrders = c.Orders.Count() // SUBQUERY AGAIN IN SELECT
                })
                .ToArray();

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("A");
            result[0].TotalOrders.Should().Be(2);
        }
    }

    [Fact]
    public void Customers_Where_Min_Order_Total_Is_Above_Threshold()
    {
        using (var uow = _fx.NewUow())
        {
            Cleanup(uow);

            var view = new XPView(uow, typeof(TestCustomer))
            {
                Criteria = CriteriaOperator.Parse("[Orders][].Count() >= 2")
            };

            view.Properties.Add(new ViewProperty("Name", SortDirection.None, "Name", false, true));
            view.Properties.Add(new ViewProperty("TotalOrders", SortDirection.None, "[Orders][].Count()", false, true));

            var result =
                uow.Query<TestOrder>()
                  .Where(o => o.Customer != null)
                  .GroupBy(o => new { o.Customer!.Oid, o.Customer!.Name })
                  .Where(g => g.Count() >= 2)
                  .Select(g => new
                  {
                      Name = g.Key.Name,
                      TotalOrders = g.Count()
                  })
                  .ToArray();

            result.Should().HaveCount(0);
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var o in new XPCollection<TestOrder>(uow).ToList()) o.Delete();
        foreach (var c in new XPCollection<TestCustomer>(uow).ToList()) c.Delete();
        uow.CommitChanges();
    }
}
