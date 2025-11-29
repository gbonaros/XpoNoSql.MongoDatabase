using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class StringExprTrimTests
{
    private readonly DbFixture _fx;
    public StringExprTrimTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Trim_Equals_NormalizesWhitespace()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "  Alpha  " };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = " Gamma" };
        uow.CommitChanges();

        var trim = new FunctionOperator(FunctionOperatorType.Trim, new OperandProperty("Name"));
        var crit = new BinaryOperator(trim, new OperandValue("Alpha"), BinaryOperatorType.Equal);

        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).ToList();
        res.Should().Equal(new[] { "  Alpha  " });
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
