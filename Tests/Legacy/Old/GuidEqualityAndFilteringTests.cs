using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class GuidEqualityAndFilteringTests
{
    private readonly DbFixture _fx;
    public GuidEqualityAndFilteringTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void FilterByGuid_ExactMatch()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p1 = new Person(uow) { Name = "One" };
        var p2 = new Person(uow) { Name = "Two" };
        uow.CommitChanges();

        var crit = new BinaryOperator(nameof(Person.TestKey), p2.TestKey, BinaryOperatorType.Equal);
        var res = new XPCollection<Person>(uow, crit).ToList();

        res.Should().ContainSingle();
        res[0].TestKey.Should().Be(p2.TestKey);
    }

    [Fact]
    public void FilterByGuid_InSet()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p1 = new Person(uow) { Name = "A" };
        var p2 = new Person(uow) { Name = "B" };
        var p3 = new Person(uow) { Name = "C" };
        uow.CommitChanges();

        var crit = new InOperator(nameof(Person.TestKey), new object[] { p1.TestKey, p3.TestKey });
        var res = new XPCollection<Person>(uow, crit).Select(p => p.TestKey).ToList();

        res.Should().BeEquivalentTo(new[] { p1.TestKey, p3.TestKey });
    }

    [Fact]
    public void Guid_StringEquality_IsDifferentFromNameString()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p = new Person(uow) { Name = "guid-like-string" };
        uow.CommitChanges();

        // using a random Guid equals shouldn't match the Name field by accident
        var bogusGuid = Guid.NewGuid();
        var crit = new BinaryOperator(nameof(Person.TestKey), bogusGuid, BinaryOperatorType.Equal);
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
