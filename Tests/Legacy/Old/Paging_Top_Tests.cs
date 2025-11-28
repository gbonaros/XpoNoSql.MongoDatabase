using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class Paging_Top_Tests
{
    private readonly DbFixture _fx;
    public Paging_Top_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void NonGrouped_Ordered_By_UpperName_Asc_Takes_First2_InMemory()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new AppUser(uow) { UserName = "beta" };
        new AppUser(uow) { UserName = "Alpha" };
        new AppUser(uow) { UserName = "gamma" };
        new AppUser(uow) { UserName = "delta" };
        uow.CommitChanges();

        // SELECT Upper([UserName]) as UP, UserName as UN
        var xp = new XPView(uow, typeof(AppUser));
        xp.Properties.Add(new ViewProperty("UP", SortDirection.None, "Upper([UserName])", false, true));
        xp.Properties.Add(new ViewProperty("UN", SortDirection.None, "UserName", false, true));

        // ORDER BY alias "UP"
        xp.Sorting.Add(new SortProperty(new OperandProperty("UP"), SortingDirection.Ascending));

        // Take the top 2 on our side to avoid the undefined TOP behavior
        var rows = xp.Cast<ViewRecord>()
                     .Select(r => (string)r["UN"])
                     .Take(2)
                     .ToList();

        // Now this is deterministic: ALPHA, BETA, DELTA, GAMMA => Alpha, beta
        rows.Should().Equal("Alpha", "beta");
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
