using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;
[Collection(XpoCollection.Name)]
public class XPViewAggregates_HavingTests
{
    private readonly DbFixture _fx;
    public XPViewAggregates_HavingTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void GroupCriteria_Count_GreaterOrEqual()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        // Two roles for george, one for maria
        var editor = new AppRole(uow) { Name = "Editor" };
        var dev = new AppRole(uow) { Name = "Dev" };
        var admin = new AppRole(uow) { Name = "Admin" };
        var george = new AppUser(uow) { UserName = "george", IsActive = true };
        var maria = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();

        new UserRole(uow) { User = george, Role = editor };
        new UserRole(uow) { User = george, Role = admin };
        new UserRole(uow) { User = maria, Role = dev };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.Ascending, "User.UserName", true, true)); // group by
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true)); // agg

        // HAVING Count() >= 2
        view.GroupCriteria = CriteriaOperator.Parse("Count() >= 2");

        var rows = view.Cast<ViewRecord>()
            .Select(r => new { U = (string)r["UserName"], C = (int)r["Cnt"] })
            .ToList();

        rows.Should().BeEquivalentTo(new[] { new { U = "george", C = 2 } });
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
