using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class PagingEdgeCasesTests
{
    private readonly DbFixture _fx;
    public PagingEdgeCasesTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void TopZero_MeansNoLimit_ReturnsAll()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "A" };
        new Person(uow) { Name = "B" };
        new Person(uow) { Name = "C" };
        uow.CommitChanges();

        var all = new XPCollection<Person>(uow).ToList();

        var coll = new XPCollection<Person>(uow) { TopReturnedObjects = 0 }; // 0 = unlimited
        var rows = coll.ToList();

        rows.Count.Should().Be(all.Count);   // same size as unbounded
        rows.Should().BeEquivalentTo(all);   // same items (order not guaranteed)
    }


    [Fact]
    public void DescendingSort_DeterministicFirstLast()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        new Person(uow) { Name = "Delta" };
        uow.CommitChanges();

        var coll = new XPCollection<Person>(uow)
        {
            Sorting = { new SortProperty(nameof(Person.Name), SortingDirection.Descending) }
        }.Select(p => p.Name).ToList();

        coll.First().Should().Be("Gamma");
        coll.Last().Should().Be("Alpha");
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
