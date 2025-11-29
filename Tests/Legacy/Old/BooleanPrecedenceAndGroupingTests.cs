using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class BooleanPrecedenceAndGroupingTests
{
    private readonly DbFixture _fx;
    public BooleanPrecedenceAndGroupingTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Parentheses_Control_Precedence_WithNumericCriteria()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Three rows with distinct (A,B)
        // m1: A=1,B=0
        // m2: A=2,B=1
        // m3: A=3,B=1
        var m1 = new Metric(uow) { A = 1, B = 0, Flags = 0 };
        var m2 = new Metric(uow) { A = 2, B = 1, Flags = 0 };
        var m3 = new Metric(uow) { A = 3, B = 1, Flags = 0 };
        uow.CommitChanges();

        // Without parentheses: A=1 OR (A=2 AND B=1)  -> matches m1 and m2
        var noParensCrit = CriteriaOperator.Parse("[A] = 1 Or [A] = 2 And [B] = 1");
        var noParens = new XPCollection<Metric>(uow, noParensCrit).Select(x => x.Oid).OrderBy(x => x).ToList();

        // With parentheses: (A=1 OR A=2) AND B=1     -> matches ONLY m2
        var withParensCrit = CriteriaOperator.Parse("([A] = 1 Or [A] = 2) And [B] = 1");
        var withParens = new XPCollection<Metric>(uow, withParensCrit).Select(x => x.Oid).OrderBy(x => x).ToList();

        noParens.Should().BeEquivalentTo(new[] { m1.Oid, m2.Oid });
        withParens.Should().BeEquivalentTo(new[] { m2.Oid });

        // And explicitly verify they are different sets
        noParens.Should().NotBeEquivalentTo(withParens);
    }

    //[Fact]
    //public void Nor_NotGroup_Behavior()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    uow.CommitChanges();

    //    // Not( Name Like 'Al%' Or Name Like 'Ga%' )  -> expect Beta, Delta
    //    var crit = CriteriaOperator.Parse("Not (Name Like 'Al%' Or Name Like 'Ga%')");
    //    var res = new XPCollection<Person>(uow, crit).Select(x => x.Name).OrderBy(x => x).ToList();

    //    res.Should().Equal(new[] { "Beta", "Delta" });
    //}

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
