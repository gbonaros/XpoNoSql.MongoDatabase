using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class NinAndNotInTests
{
    private readonly DbFixture _fx;
    public NinAndNotInTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Not_In_Strings_Equivalent_To_Exclude_Set()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        new Person(uow) { Name = "Delta" };
        uow.CommitChanges();

        var notIn = new XPCollection<Person>(uow,
            CriteriaOperator.Parse("Not ([Name] In ('Alpha','Gamma'))"))
            .Select(p => p.Name).OrderBy(x => x).ToList();

        notIn.Should().Equal(new[] { "Beta", "Delta" });
    }

    [Fact]
    public void Not_In_Guids_Selects_Complement()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p1 = new Person(uow) { Name = "A" };
        var p2 = new Person(uow) { Name = "B" };
        var p3 = new Person(uow) { Name = "C" };
        var p4 = new Person(uow) { Name = "D" };
        uow.CommitChanges();

        var crit = new UnaryOperator(UnaryOperatorType.Not,
            new InOperator(nameof(Person.TestKey), new object[] { p1.TestKey, p3.TestKey }));

        var res = new XPCollection<Person>(uow, crit)
            .Select(p => p.TestKey).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { p2.TestKey, p4.TestKey }.OrderBy(x => x));
    }

    [Fact]
    public void In_With_Duplicates_Behaves_As_Set()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var a = new Person(uow) { Name = "A" };
        var b = new Person(uow) { Name = "B" };
        var c = new Person(uow) { Name = "C" };
        uow.CommitChanges();

        var crit = new InOperator(nameof(Person.Name), new object[] { "A", "A", "C" });
        var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

        res.Should().Equal(new[] { "A", "C" });
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
