using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class ExistsAndNullParityMoreTests
{
    private readonly DbFixture _fx;
    public ExistsAndNullParityMoreTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void DoubleNot_IsNull_Equals_IsNull()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var pNull = new Person(uow) { Name = null };
        var pA = new Person(uow) { Name = "A" };
        uow.CommitChanges();

        var a = new XPCollection<Person>(uow, CriteriaOperator.Parse("[Name] Is Null")).Select(x => x.TestKey).ToHashSet();
        var b = new XPCollection<Person>(uow, CriteriaOperator.Parse("Not (Not ([Name] Is Null))")).Select(x => x.TestKey).ToHashSet();

        b.Should().BeEquivalentTo(a);
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
