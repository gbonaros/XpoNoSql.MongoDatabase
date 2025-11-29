using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class OrderGroupByMultipleKeysTests
{
    private readonly DbFixture _fx;
    public OrderGroupByMultipleKeysTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Group_Orders_By_Customer_And_Filter_Groups_With_Total_Over_Threshold()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            var alice = new TestCustomer(uow) { Name = "Alice" };
            var bob = new TestCustomer(uow) { Name = "Bob" };

            new TestOrder(uow) { Customer = alice, ProductName = "A1", Quantity = 1, Total = 10 };
            new TestOrder(uow) { Customer = alice, ProductName = "A2", Quantity = 1, Total = 50 };
            new TestOrder(uow) { Customer = bob, ProductName = "B1", Quantity = 1, Total = 15 };
            new TestOrder(uow) { Customer = bob, ProductName = "B2", Quantity = 1, Total = 20 };
            uow.CommitChanges();

            decimal threshold = 40;

            // XPView over TestOrder
            var view = new XPView(uow, typeof(TestOrder))
            {
                // WHERE Customer IS NOT NULL
                Criteria = CriteriaOperator.Parse("Customer Is Not Null")
            };

            // Group key: Customer.Name
            view.Properties.Add(new ViewProperty(
                "CustomerName",
                SortDirection.Ascending,        // also orders by this column
                "Customer.Name",                // expression
                true,
                true));

            // Aggregate: Sum(Total)
            view.Properties.Add(new ViewProperty(
                "TotalAmount",
                SortDirection.None,
                "Sum(Total)",                    // aggregate expression
                false,
                true));

            view.GroupCriteria = CriteriaOperator.Parse("Sum([Total]) > ?", threshold);

            var result = view.Cast<ViewRecord>()
                .OrderBy(r => (string)r["CustomerName"])
                .Select(r => new
                {
                    CustomerName = (string)r["CustomerName"],
                    TotalAmount = (decimal)r["TotalAmount"]
                })
                .ToArray();

            // Assertions
            result.Should().HaveCount(1);
            result[0].CustomerName.Should().Be("Alice");
            result[0].TotalAmount.Should().Be(60m);
        }
    }
    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
