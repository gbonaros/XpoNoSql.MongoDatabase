
namespace XpoNoSql.Tests;

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

