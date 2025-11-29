using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

namespace XpoNoSql.Tests;

using System;
using System.Linq;

using Xunit;

[Collection(XpoCollection.Name)]
public class AdvancedSortingAndIdentityTests
{
    private readonly DbFixture _fx;
    public AdvancedSortingAndIdentityTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void MultiColumnSorting_SkipTop_EdgeCases()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Gamma" };
        new Person(uow) { Name = "Delta" };
        uow.CommitChanges();

        var coll = new XPCollection<Person>(uow)
        {
            Sorting =
            {
                new SortProperty(nameof(Person.Name), SortingDirection.Ascending),
                // Stable 2nd sort: by key descending to ensure deterministic page cut
                new SortProperty(nameof(Person.TestKey), SortingDirection.Descending)
            },
            SkipReturnedObjects = 1,
            TopReturnedObjects = 2
        };
        coll.Select(p => p.Name).Should().Equal(new[] { "Beta", "Delta" });

        // Skip beyond count returns none
        var beyond = new XPCollection<Person>(uow)
        {
            Sorting = { new SortProperty(nameof(Person.Name), SortingDirection.Ascending) },
            SkipReturnedObjects = 10,
            TopReturnedObjects = 5
        };
        beyond.Should().BeEmpty();
    }

    [Fact]
    public void SessionIdentity_SameInstanceWithinUnitOfWork()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p = new Person(uow) { Name = "Alpha" };
        uow.CommitChanges();

        var byKey1 = uow.GetObjectByKey<Person>(p.TestKey);
        var byKey2 = uow.GetObjectByKey<Person>(p.TestKey);

        // Same in-memory instance inside a single UoW identity map
        object.ReferenceEquals(byKey1, byKey2).Should().BeTrue();

        // After reload it stays the same instance
        uow.Reload(byKey1);
        object.ReferenceEquals(byKey1, byKey2).Should().BeTrue();
    }

    [Fact]
    public void UpdateChain_MultipleCommits_PersistsFinalState()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p = new Person(uow) { Name = "A" };
        uow.CommitChanges();

        p.Name = "B";
        uow.CommitChanges();

        p.Name = "C";
        uow.CommitChanges();

        var fresh = uow.GetObjectByKey<Person>(p.TestKey);
        fresh.Name.Should().Be("C");
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
