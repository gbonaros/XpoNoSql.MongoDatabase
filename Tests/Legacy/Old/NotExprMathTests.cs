using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class NotExprMathTests
{
    private readonly DbFixture _fx;
    public NotExprMathTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Not_On_Expr_Gte_Becomes_Lt()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Sums: 10, 12, 3
        var m1 = new Metric(uow) { A = 5, B = 5, Flags = 0 };
        var m2 = new Metric(uow) { A = 10, B = 2, Flags = 0 };
        var m3 = new Metric(uow) { A = 1, B = 2, Flags = 0 };
        uow.CommitChanges();

        // Not ((A+B) >= 10)  → should match only m3
        var notExpr = new XPCollection<Metric>(uow,
            new UnaryOperator(UnaryOperatorType.Not,
                new BinaryOperator(
                    new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Plus),
                    new OperandValue(10),
                    BinaryOperatorType.GreaterOrEqual)))
            .Select(m => m.Oid).OrderBy(x => x).ToList();

        notExpr.Should().Equal(new[] { m3.Oid });
    }

    [Fact]
    public void Not_On_Expr_Mul_Less()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Products: 25, 20, 2
        var m1 = new Metric(uow) { A = 5, B = 5, Flags = 0 };
        var m2 = new Metric(uow) { A = 10, B = 2, Flags = 0 };
        var m3 = new Metric(uow) { A = 1, B = 2, Flags = 0 };
        uow.CommitChanges();

        // Not ((A*B) < 10) → matches products >= 10 → m1 (25), m2 (20)
        var notExpr = new XPCollection<Metric>(uow,
            new UnaryOperator(UnaryOperatorType.Not,
                new BinaryOperator(
                    new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Multiply),
                    new OperandValue(10),
                    BinaryOperatorType.Less)))
            .Select(m => m.Oid).OrderBy(x => x).ToList();

        notExpr.Should().Equal(new[] { m1.Oid, m2.Oid });
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<UserRole>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Kid>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppUser>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppRole>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Person>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<Metric>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
