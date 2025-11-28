using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class NullAndExistsParityTests
{
    private readonly DbFixture _fx;
    public NullAndExistsParityTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void IsNull_Matches_Null_And_Missing()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Name=null (XPO typically omits the field, so it's "missing" on disk)
        var pNull = new Person(uow) { Name = null };
        // Name explicitly set to empty string
        var pEmpty = new Person(uow) { Name = string.Empty };
        var pAlpha = new Person(uow) { Name = "Alpha" };
        uow.CommitChanges();

        // [Name] Is Null → should include doc where field is null or absent
        var isNull = new XPCollection<Person>(uow, CriteriaOperator.Parse("[Name] Is Null")).ToList();

        isNull.Should().Contain(x => x.TestKey == pNull.TestKey);
        isNull.Should().NotContain(x => x.TestKey == pEmpty.TestKey);
        isNull.Should().NotContain(x => x.TestKey == pAlpha.TestKey);
    }

    [Fact]
    public void Not_IsNull_Excludes_Null_And_Missing()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var pNull = new Person(uow) { Name = null };
        var pEmpty = new Person(uow) { Name = string.Empty };
        var pA = new Person(uow) { Name = "A" };
        uow.CommitChanges();

        var notIsNull = new XPCollection<Person>(uow, CriteriaOperator.Parse("Not ([Name] Is Null)")).ToList();

        notIsNull.Should().Contain(x => x.TestKey == pEmpty.TestKey);
        notIsNull.Should().Contain(x => x.TestKey == pA.TestKey);
        notIsNull.Should().NotContain(x => x.TestKey == pNull.TestKey);
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
