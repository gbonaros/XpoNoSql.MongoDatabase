using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class StringExprIndexOfTests
{
    private readonly DbFixture _fx;
    public StringExprIndexOfTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void CharIndex_Finds_Substring_Positive()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        uow.CommitChanges();

        // CharIndex('mm', Lower(Name)) >= 0  -> matches "Gamma" only
        var lower = new FunctionOperator(FunctionOperatorType.Lower, new OperandProperty("Name"));
        var idx = new FunctionOperator(FunctionOperatorType.CharIndex, new OperandValue("mm"), lower);

        var crit = new BinaryOperator(idx, new OperandValue(0), BinaryOperatorType.GreaterOrEqual);
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).ToList();

        res.Should().Equal(new[] { "Gamma" });
    }

    [Fact]
    public void Not_CharIndex_Negative()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        uow.CommitChanges();

        // Not (CharIndex('z', Name) >= 0) -> names without 'z' (i.e., all here)
        var idx = new FunctionOperator(FunctionOperatorType.CharIndex, new OperandValue("z"), new OperandProperty("Name"));
        var cond = new BinaryOperator(idx, new OperandValue(0), BinaryOperatorType.Greater);

        var notCrit = new UnaryOperator(UnaryOperatorType.Not, cond);
        var res = new XPCollection<Person>(uow, notCrit).Select(p => p.Name).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { "Alpha", "Beta" });
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
