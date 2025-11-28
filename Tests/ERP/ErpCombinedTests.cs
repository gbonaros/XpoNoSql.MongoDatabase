
using DevExpress.Xpo;

using FluentAssertions;

using System;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.ERP;

[Collection(XpoCollection.Name)]
public class ErpCombinedTests
{
    private readonly DbFixture _fx;

    public ErpCombinedTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Region_Filter_Group_Having_Order()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var east = new ErpCustomer(uow) { Name = "East", Region = "East", Status = "Active" };
        var west = new ErpCustomer(uow) { Name = "West", Region = "West", Status = "Active" };

        new ErpOrder(uow) { OrderNumber = "E1", OrderDate = DateTime.UtcNow, Status = "Closed", TotalAmount = 400m, Customer = east };
        new ErpOrder(uow) { OrderNumber = "E2", OrderDate = DateTime.UtcNow, Status = "Closed", TotalAmount = 300m, Customer = east };
        new ErpOrder(uow) { OrderNumber = "W1", OrderDate = DateTime.UtcNow, Status = "Closed", TotalAmount = 100m, Customer = west };
        uow.CommitChanges();

        var grouped = uow.Query<ErpOrder>()
            .Where(o => o.Customer != null && o.Status == "Closed")
            .GroupBy(o => o.Customer!.Region)
            .Select(g => new { Region = g.Key, Total = g.Sum(o => o.TotalAmount) })
            .Where(r => r.Total >= 500m)
            .OrderByDescending(r => r.Total)
            .ToArray();

        grouped.Should().HaveCount(1);
        grouped[0].Region.Should().Be("East");
        grouped[0].Total.Should().Be(700m);
    }

    [Fact]
    public void Lines_With_Join_To_Product_And_Customer_Filter()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var cust = new ErpCustomer(uow) { Name = "Customer", Region = "Central", Status = "Active" };
        var prod = new ErpProduct(uow) { Name = "License", Category = "Software", UnitPrice = 100m, IsActive = true };
        var order = new ErpOrder(uow) { OrderNumber = "C1", Status = "Closed", Customer = cust, TotalAmount = 0m };
        new ErpOrderLine(uow) { LineNumber = 1, Quantity = 1, LineTotal = 100m, Order = order, Product = prod };
        new ErpOrderLine(uow) { LineNumber = 2, Quantity = 3, LineTotal = 300m, Order = order, Product = prod };
        order.TotalAmount = 400m;
        uow.CommitChanges();

        var lines = uow.Query<ErpOrderLine>()
            .Where(l => l.Order != null && l.Order.Customer != null && l.Order.Customer.Region == "Central" && l.Product != null && l.Product.Category == "Software")
            .OrderByDescending(l => l.LineTotal)
            .Select(l => new { l.LineNumber, l.LineTotal, Customer = l.Order!.Customer!.Name, Product = l.Product!.Name })
            .ToArray();

        lines.Should().HaveCount(2);
        lines[0].LineTotal.Should().Be(300m);
        lines[0].Customer.Should().Be("Customer");
        lines[0].Product.Should().Be("License");
    }

    [Fact]
    public void CreditLimit_Filter_And_OrderCount_Per_Customer()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var high = new ErpCustomer(uow) { Name = "High", Region = "H", Status = "Active", CreditLimit = 20000m };
        var low = new ErpCustomer(uow) { Name = "Low", Region = "L", Status = "Active", CreditLimit = 1000m };

        new ErpOrder(uow) { OrderNumber = "H1", Status = "Closed", Customer = high, TotalAmount = 1000m };
        new ErpOrder(uow) { OrderNumber = "H2", Status = "Closed", Customer = high, TotalAmount = 2000m };
        new ErpOrder(uow) { OrderNumber = "L1", Status = "Closed", Customer = low, TotalAmount = 50m };
        uow.CommitChanges();

        var summary = uow.Query<ErpOrder>()
            .Where(o => o.Customer != null && o.Customer.CreditLimit >= 5000m)
            .GroupBy(o => new { o.Customer!.Name, o.Customer.CreditLimit })
            .Select(g => new { g.Key.Name, g.Key.CreditLimit, Count = g.Count(), Sum = g.Sum(o => o.TotalAmount) })
            .OrderByDescending(r => r.Sum)
            .ToArray();

        summary.Should().HaveCount(1);
        summary[0].Name.Should().Be("High");
        summary[0].Count.Should().Be(2);
        summary[0].Sum.Should().Be(3000m);
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}
