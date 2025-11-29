using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class ModuloEvenOddAndMathTests
{
    private readonly DbFixture _fx;
    public ModuloEvenOddAndMathTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Modulo_EvenOdd_Filters()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Metric(uow) { A = 1, B = 0, Flags = 0 };
        new Metric(uow) { A = 2, B = 0, Flags = 0 };
        new Metric(uow) { A = 3, B = 0, Flags = 0 };
        new Metric(uow) { A = 4, B = 0, Flags = 0 };
        uow.CommitChanges();

        var evens = new XPCollection<Metric>(uow, CriteriaOperator.Parse("[A] % 2 = 0"));
        evens.Select(m => m.A).Should().BeEquivalentTo(new[] { 2, 4 });

        var odds = new XPCollection<Metric>(uow, CriteriaOperator.Parse("[A] % 2 <> 0"));
        odds.Select(m => m.A).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void MixedArithmetic_ChainedComparison()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Metric(uow) { A = 10, B = 5, Flags = 0 }; // (A+B) - (A-B) = 10
        new Metric(uow) { A = 9, B = 3, Flags = 0 }; // (12) - (6) = 6
        new Metric(uow) { A = 4, B = 2, Flags = 0 }; // (6) - (2) = 4
        uow.CommitChanges();

        var crit = CriteriaOperator.Parse("([A] + [B]) - ([A] - [B]) >= 6");
        var res = new XPCollection<Metric>(uow, crit).Select(m => m.A).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { 9, 10 });
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
