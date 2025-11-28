using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class StringExprCaseTests
{
    private readonly DbFixture _fx;
    public StringExprCaseTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Lower_Equals_CaseInsensitive_Compare()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "BETA" };
        new Person(uow) { Name = "Gamma" };
        uow.CommitChanges();

        // Lower(Name) = 'beta'  -> matches "BETA"
        var lc = new FunctionOperator(FunctionOperatorType.Lower, new OperandProperty("Name"));
        var crit = new BinaryOperator(lc, new OperandValue("beta"), BinaryOperatorType.Equal);

        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).ToList();
        res.Should().Equal(new[] { "BETA" });
    }

    [Fact]
    public void Upper_Substring_Equals()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "alpha" };
        new Person(uow) { Name = "beta" };
        new Person(uow) { Name = "gamma" };
        uow.CommitChanges();

        // Substring(Upper(Name), 0, 2) = 'AL' -> matches "alpha"
        var up = new FunctionOperator(FunctionOperatorType.Upper, new OperandProperty("Name"));
        var sub = new FunctionOperator(FunctionOperatorType.Substring, up, new OperandValue(0), new OperandValue(2));

        var crit = new BinaryOperator(sub, new OperandValue("AL"), BinaryOperatorType.Equal);
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).ToList();

        res.Should().Equal(new[] { "alpha" });
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
