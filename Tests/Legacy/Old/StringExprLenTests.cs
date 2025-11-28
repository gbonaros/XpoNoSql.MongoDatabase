using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class StringExprLenTests
{
    private readonly DbFixture _fx;
    public StringExprLenTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Len_GreaterOrEqual_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Al" };        // len 2
        new Person(uow) { Name = "Alpha" };     // len 5
        new Person(uow) { Name = "GammaRay" };  // len 8
        uow.CommitChanges();

        var crit = new BinaryOperator(
            new FunctionOperator(FunctionOperatorType.Len, new OperandProperty("Name")),
            new OperandValue(5),
            BinaryOperatorType.GreaterOrEqual);

        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();
        res.Should().Equal(new[] { "Alpha", "GammaRay" });
    }

    [Fact]
    public void Not_Len_LessThan_Acts_As_GreaterOrEqual()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "A" };     // 1
        new Person(uow) { Name = "Beta" };  // 4
        new Person(uow) { Name = "Delta" }; // 5
        uow.CommitChanges();

        // Not (Len(Name) < 5) -> length >= 5
        var inner = new BinaryOperator(
            new FunctionOperator(FunctionOperatorType.Len, new OperandProperty("Name")),
            new OperandValue(5),
            BinaryOperatorType.Less);

        var notCrit = new UnaryOperator(UnaryOperatorType.Not, inner);

        var res = new XPCollection<Person>(uow, notCrit).Select(p => p.Name).OrderBy(x => x).ToList();
        res.Should().Equal(new[] { "Delta" });
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
