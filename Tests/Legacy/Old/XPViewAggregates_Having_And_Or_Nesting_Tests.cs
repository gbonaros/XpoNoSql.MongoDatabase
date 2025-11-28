using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;
[Collection(XpoCollection.Name)]
public class XPViewAggregates_Having_And_Or_Nesting_Tests
{
    private readonly DbFixture _fx;
    public XPViewAggregates_Having_And_Or_Nesting_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Having_And_Or_Nested_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Ensure two distinct OIDs: rLow < rHigh
        var rLow = new AppRole(uow) { Name = "Low" };
        var rHigh = new AppRole(uow) { Name = "High" };
        var g1 = new AppUser(uow) { UserName = "g1" };
        var g2 = new AppUser(uow) { UserName = "g2" };
        var g3 = new AppUser(uow) { UserName = "g3" };
        uow.CommitChanges();

        // g1: two High -> Count=2, MinRole = rHigh.Oid (>= threshold)
        new UserRole(uow) { User = g1, Role = rHigh };
        new UserRole(uow) { User = g1, Role = rHigh };

        // g2: one High -> Count=1
        new UserRole(uow) { User = g2, Role = rHigh };

        // g3: one Low + one High -> Count=2, MinRole = rLow.Oid (< threshold)
        new UserRole(uow) { User = g3, Role = rHigh };
        new UserRole(uow) { User = g3, Role = rLow };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole))
        {
            Properties =
        {
            new ViewProperty("UserName", SortDirection.None, "User.UserName", true,  true),
            new ViewProperty("Cnt",      SortDirection.None, "Count()",       false, true),
            new ViewProperty("MinRole",  SortDirection.None, "Min([Role.Oid])", false, true),
        },
            // HAVING: (Count >= 2 AND Min(Role.Oid) >= rHigh.Oid) OR (Count = 1)
            GroupCriteria = CriteriaOperator.Parse($"(Count() >= 2 AND Min([Role.Oid]) >= {rHigh.Oid}) OR (Count() = 1)")
        };

        var rows = view.Cast<ViewRecord>()
            .Select(rw => new
            {
                U = rw["UserName"] as string,
                C = rw["Cnt"] switch
                {
                    long l => (int)l,
                    int i => i,
                    double d => (int)d,
                    _ => Convert.ToInt32(rw["Cnt"])
                },
                MinR = rw["MinRole"] switch
                {
                    long l => (int)l,
                    int i => i,
                    double d => (int)d,
                    _ => Convert.ToInt32(rw["MinRole"])
                }
            })
            .OrderBy(x => x.U)
            .ToList();

        rows.Should().BeEquivalentTo(new[]
        {
        new { U = "g1", C = 2, MinR = rHigh.Oid },
        new { U = "g2", C = 1, MinR = rHigh.Oid },
    }, opts => opts.WithStrictOrdering());
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


