using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class InAndGuidSetsTests
{
    private readonly DbFixture _fx;
    public InAndGuidSetsTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void In_On_Strings_BasicSubset()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        new Person(uow) { Name = "Delta" };
        uow.CommitChanges();

        var crit = CriteriaOperator.Parse("Name In ('Alpha','Gamma')");
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { "Alpha", "Gamma" });
    }

    [Fact]
    public void In_On_Guids_SelectsExactKeys()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p1 = new Person(uow) { Name = "A" };
        var p2 = new Person(uow) { Name = "B" };
        var p3 = new Person(uow) { Name = "C" };
        uow.CommitChanges();

        var crit = new InOperator(nameof(Person.TestKey), new object[] { p1.TestKey, p3.TestKey });
        var res = new XPCollection<Person>(uow, crit).Select(p => p.TestKey).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { p1.TestKey, p3.TestKey }.OrderBy(x => x));
    }

    [Fact]
    public void In_EmptySet_MatchesNothing()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "A" };
        new Person(uow) { Name = "B" };
        uow.CommitChanges();

        var crit = new InOperator(nameof(Person.Name), Array.Empty<object>());
        var res = new XPCollection<Person>(uow, crit).ToList();

        res.Should().BeEmpty();
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
