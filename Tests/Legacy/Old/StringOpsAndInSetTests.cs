using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class StringOpsAndInSetTests
{
    private readonly DbFixture _fx;
    public StringOpsAndInSetTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void StartsWith_EndsWith_Functions_Work()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "alpha" };
        new Person(uow) { Name = "beta" };
        new Person(uow) { Name = "gamma" };
        new Person(uow) { Name = "delta" };
        uow.CommitChanges();

        var starts = new XPCollection<Person>(uow, CriteriaOperator.Parse("StartsWith([Name], 'ga')"));
        starts.Select(p => p.Name).Should().BeEquivalentTo(new[] { "gamma" });

        var ends = new XPCollection<Person>(uow, CriteriaOperator.Parse("EndsWith([Name], 'ta')"));
        ends.Select(p => p.Name).Should().BeEquivalentTo(new[] { "beta", "delta" });
    }

    [Fact]
    public void InSet_OnNumeric_And_String()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Metric(uow) { A = 1, B = 0, Flags = 3 };
        new Metric(uow) { A = 2, B = 0, Flags = 4 };
        new Metric(uow) { A = 3, B = 0, Flags = 8 };
        uow.CommitChanges();

        var flagsIn = new XPCollection<Metric>(uow, CriteriaOperator.Parse("Flags In (3,4)"));
        flagsIn.Select(m => m.Flags).Should().BeEquivalentTo(new[] { 3, 4 });

        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = "Gamma" };
        uow.CommitChanges();

        var namesIn = new XPCollection<Person>(uow, CriteriaOperator.Parse("Name In ('Alpha','Gamma')"));
        namesIn.Select(p => p.Name).Should().BeEquivalentTo(new[] { "Alpha", "Gamma" });
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
