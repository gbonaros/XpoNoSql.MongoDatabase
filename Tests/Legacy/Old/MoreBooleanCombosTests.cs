using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;
[Collection(XpoCollection.Name)]
public class MoreBooleanCombosTests
{
    private readonly DbFixture _fx;
    public MoreBooleanCombosTests(DbFixture fx) => _fx = fx;

    //[Fact]
    //public void ThreeClause_Mix_OrAndNot()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    new Person(uow) { Name = "Alpha" };
    //    new Person(uow) { Name = "Beta" };
    //    new Person(uow) { Name = "Gamma" };
    //    new Person(uow) { Name = "Delta" };
    //    new Person(uow) { Name = "Zeta" };
    //    uow.CommitChanges();

    //    // (Name Like 'A%' Or Name Like 'G%') And Not (Name Like '%ta')
    //    var crit = CriteriaOperator.Parse("(Name Like 'A%' Or Name Like 'G%') And Not (Name Like '%ta')");
    //    var res = new XPCollection<Person>(uow, crit).Select(p => p.Name).OrderBy(x => x).ToList();

    //    res.Should().Equal(new[] { "Alpha", "Gamma" });
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
