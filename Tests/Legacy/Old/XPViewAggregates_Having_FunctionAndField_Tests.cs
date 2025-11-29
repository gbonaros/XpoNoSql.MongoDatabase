using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;
[Collection(XpoCollection.Name)]
public class XPViewAggregates_Having_FunctionAndField_Tests
{
    private readonly DbFixture _fx;
    public XPViewAggregates_Having_FunctionAndField_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Having_Count_GreaterOrEqual_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var editor = new AppRole(uow) { Name = "Editor" };
        var admin = new AppRole(uow) { Name = "Admin" };
        var dev = new AppRole(uow) { Name = "Dev" };

        var george = new AppUser(uow) { UserName = "george", IsActive = true };
        var maria = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();

        new UserRole(uow) { User = george, Role = editor };
        new UserRole(uow) { User = george, Role = admin };
        new UserRole(uow) { User = maria, Role = dev };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", true, true)); // group by
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true)); // agg

        view.GroupCriteria = CriteriaOperator.Parse("Count() >= 2"); // HAVING

        var rows = view.Cast<ViewRecord>()
                       .Select(r => new { U = (string)r["UserName"], C = (int)r["Cnt"] })
                       .ToList();

        rows.Should().BeEquivalentTo(new[] { new { U = "george", C = 2 } });
    }

    [Fact]
    public void Having_On_Grouped_Field_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var role = new AppRole(uow) { Name = "R" };
        var g1 = new AppUser(uow) { UserName = "george", IsActive = true };
        var g2 = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();

        new UserRole(uow) { User = g1, Role = role };
        new UserRole(uow) { User = g2, Role = role };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", true, true));
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true));

        view.GroupCriteria = CriteriaOperator.Parse("[User.UserName] = 'george'");

        var rows = view.Cast<ViewRecord>().Select(r => (string)r["UserName"]).ToList();
        rows.Should().Equal("george");
    }

    [Fact]
    public void Having_Combined_Field_And_Aggregate_Works()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var r1 = new AppRole(uow) { Name = "Editor" };
        var r2 = new AppRole(uow) { Name = "Admin" };
        var r3 = new AppRole(uow) { Name = "Dev" };
        var g1 = new AppUser(uow) { UserName = "george", IsActive = true };
        var g2 = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();

        new UserRole(uow) { User = g1, Role = r1 };
        new UserRole(uow) { User = g1, Role = r2 };
        new UserRole(uow) { User = g2, Role = r3 };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", true, true));
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true));
        view.Properties.Add(new ViewProperty("MinRole", SortDirection.None, "Min([Role.Oid])", false, true));

        view.GroupCriteria = CriteriaOperator.Parse("[User.UserName] = 'george' AND Min([Role.Oid]) >= 1");

        var rows = view.Cast<ViewRecord>()
                       .Select(r => new { U = (string)r["UserName"], C = (int)r["Cnt"], MinRole = (int)r["MinRole"] })
                       .ToList();

        rows.Should().BeEquivalentTo(new[] { new { U = "george", C = 2, MinRole = r1.Oid } },
            opts => opts.ExcludingMissingMembers());
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
