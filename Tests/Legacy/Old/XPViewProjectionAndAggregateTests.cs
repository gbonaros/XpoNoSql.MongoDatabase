using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;
[Collection(XpoCollection.Name)]
public class XPViewProjectionAndAggregateTests
{
    private readonly DbFixture _fx;
    public XPViewProjectionAndAggregateTests(DbFixture fx) => _fx = fx;

    //[Fact]
    //public void XPView_Project_Joined_Fields_And_Filter()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    var admin = new AppRole(uow) { Name = "Admin" };
    //    var editor = new AppRole(uow) { Name = "Editor" };
    //    var dev = new AppRole(uow) { Name = "Dev" };
    //    var g1 = new AppUser(uow) { UserName = "george", IsActive = true };
    //    var m1 = new AppUser(uow) { UserName = "maria", IsActive = true };
    //    var x1 = new AppUser(uow) { UserName = "x", IsActive = false };
    //    uow.CommitChanges();

    //    new UserRole(uow) { User = g1, Role = admin };
    //    new UserRole(uow) { User = g1, Role = editor };
    //    new UserRole(uow) { User = m1, Role = editor };
    //    new UserRole(uow) { User = m1, Role = dev };
    //    new UserRole(uow) { User = x1, Role = admin };
    //    uow.CommitChanges();

    //    var view = new XPView(uow, typeof(UserRole))
    //    {
    //        Criteria = CriteriaOperator.Parse("[User.IsActive] = True And ([Role.Name] Like 'E%')")
    //    };
    //    view.Properties.Add(new ViewProperty("UserName", SortDirection.Ascending, "User.UserName", false, true));
    //    view.Properties.Add(new ViewProperty("RoleName", SortDirection.Ascending, "Role.Name", false, true));

    //    var rows = view.Cast<ViewRecord>().Select(r => new { U = (string)r["UserName"], R = (string)r["RoleName"] }).ToList();
    //    rows.Should().ContainSingle(x => x.U == "george" && x.R == "Editor");
    //    rows.Should().ContainSingle(x => x.U == "maria" && x.R == "Editor");
    //    rows.Should().NotContain(x => x.U == "x");
    //}

    //[Fact]
    //public void XPView_Simple_Aggregate_Count_Roles_Per_User()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    var editor = new AppRole(uow) { Name = "Editor" };
    //    var dev = new AppRole(uow) { Name = "Dev" };
    //    var g1 = new AppUser(uow) { UserName = "george", IsActive = true };
    //    var m1 = new AppUser(uow) { UserName = "maria", IsActive = true };
    //    uow.CommitChanges();

    //    new UserRole(uow) { User = g1, Role = editor };
    //    new UserRole(uow) { User = g1, Role = dev };
    //    new UserRole(uow) { User = m1, Role = editor };
    //    uow.CommitChanges();

    //    var view = new XPView(uow, typeof(UserRole));
    //    // group by User.UserName with aggregate Count()
    //    view.Properties.Add(new ViewProperty("UserName", SortDirection.Ascending, "User.UserName", true, true));
    //    view.Properties.Add(new ViewProperty("Roles", SortDirection.None, "Count()", false, true));

    //    var rows = view.Cast<ViewRecord>()
    //        .Select(r => new { U = (string)r["UserName"], C = (int)r["Roles"] })
    //        .OrderBy(x => x.U)
    //        .ToList();

    //    rows.Should().BeEquivalentTo(new[]
    //    {
    //        new { U = "george", C = 2 },
    //        new { U = "maria",  C = 1 }
    //    });
    //}

    [Fact]
    public void XPView_Projection_With_Expression()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "  Alpha  " };
        new Person(uow) { Name = "Beta" };
        new Person(uow) { Name = " Gamma" };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(Person));
        view.Properties.Add(new ViewProperty("Trimmed", SortDirection.Ascending, "Trim([Name])", false, true));
        view.Properties.Add(new ViewProperty("Len", SortDirection.None, "Len([Name])", false, true));

        var rows = view.Cast<ViewRecord>()
            .Select(r => new { T = (string)r["Trimmed"], L = (int)r["Len"] })
            .OrderBy(x => x.T)
            .ToList();

        rows.Select(x => x.T).Should().Equal(new[] { "Alpha", "Beta", "Gamma" });
        rows.Single(x => x.T == "Alpha").L.Should().Be(7);
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
