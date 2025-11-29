using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class StringExprConcatTests
{
    private readonly DbFixture _fx;
    public StringExprConcatTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Concat_Equals_ExactMatch()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        uow.CommitChanges();

        // Concat('Mr. ', Name) = 'Mr. Beta'
        var concat = new FunctionOperator(FunctionOperatorType.Concat,
            new OperandValue("Mr. "), new OperandProperty("Name"));

        var crit = new BinaryOperator(concat, new OperandValue("Mr. Beta"), BinaryOperatorType.Equal);
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).ToList();

        res.Should().Equal(new[] { "Beta" });
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
