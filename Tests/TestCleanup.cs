using MongoProvider.Tests.Models;
using MongoProvider.Tests.Entities;
using XpoNoSQL.MongoDatabase.Tests.Simple;
using XpoNoSQL.MongoDatabase.Tests.ERP;
using XpoNoSQL.MongoDatabase.Tests.ToDo;

namespace XpoNoSQL.MongoDatabase.Tests;

internal static class TestCleanup
{
    public static void ClearAll(DbFixture fx)
    {
        if (fx == null)
        {
            return;
        }

        fx.Cleanup<SimpleChild>();
        fx.Cleanup<SimpleParent>();
        fx.Cleanup<SimpleItem>();

        fx.Cleanup<TestOrder>();
        fx.Cleanup<TestCustomer>();

        fx.Cleanup<Product>();
        fx.Cleanup<Order>();
        fx.Cleanup<Customer>();

        fx.Cleanup<ErpOrderLine>();
        fx.Cleanup<ErpOrder>();
        fx.Cleanup<ErpProduct>();
        fx.Cleanup<ErpCustomer>();

        fx.Cleanup<CustomKeyObject>();
    }
}

