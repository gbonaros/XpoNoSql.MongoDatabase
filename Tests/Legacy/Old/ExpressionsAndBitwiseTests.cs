using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class ExpressionsAndBitwiseTests
{
    private readonly DbFixture _fx;
    public ExpressionsAndBitwiseTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void ArithmeticExpr_AggregationComparison()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var m1 = new Metric(uow) { A = 5, B = 5, Flags = 3 };  // A+B=10
        var m2 = new Metric(uow) { A = 10, B = 2, Flags = 4 };  // A+B=12
        var m3 = new Metric(uow) { A = 1, B = 2, Flags = 8 };  // A+B=3
        uow.CommitChanges();

        // (A + B) >= 10
        var critAdd = new BinaryOperator(
            new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Plus),
            new OperandValue(10),
            BinaryOperatorType.GreaterOrEqual);
        var qAdd = new XPCollection<Metric>(uow, critAdd).ToList();
        qAdd.Should().Contain(x => x.Oid == m1.Oid).And.Contain(x => x.Oid == m2.Oid);

        // (A * B) < 50
        var critMul = new BinaryOperator(
            new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Multiply),
            new OperandValue(50),
            BinaryOperatorType.Less);
        var qMul = new XPCollection<Metric>(uow, critMul).ToList();
        qMul.Should().Contain(x => x.Oid == m1.Oid).And.Contain(x => x.Oid == m3.Oid);
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
