using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class Join_Group_Having_Tests
{
    private readonly DbFixture _fx;
    public Join_Group_Having_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void GroupBy_Joined_User_Having_Count_And_Name_Filter()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var r1 = new AppRole(uow) { Name = "Editor" };
        var r2 = new AppRole(uow) { Name = "Admin" };

        var g1 = new AppUser(uow) { UserName = "george" };
        var g2 = new AppUser(uow) { UserName = "maria" };
        var g3 = new AppUser(uow) { UserName = "alex" };
        uow.CommitChanges();

        // george=3, maria=2, alex=1
        new UserRole(uow) { User = g1, Role = r1 };
        new UserRole(uow) { User = g1, Role = r1 };
        new UserRole(uow) { User = g1, Role = r2 };
        new UserRole(uow) { User = g2, Role = r1 };
        new UserRole(uow) { User = g2, Role = r2 };
        new UserRole(uow) { User = g3, Role = r2 };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", true, true));   // group key
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true));   // aggregate

        // HAVING: Count() >= 2 AND [User.UserName] <> 'maria'
        view.GroupCriteria = CriteriaOperator.Parse("Count() >= 2 AND [User.UserName] <> 'maria'");

        // ORDER BY Cnt DESC, UserName ASC (by aliases)
        view.Sorting.Add(new SortProperty(new OperandProperty("Cnt"), SortingDirection.Descending));
        view.Sorting.Add(new SortProperty(new OperandProperty("UserName"), SortingDirection.Ascending));

        var rows = view.Cast<ViewRecord>()
                       .Select(r => new { U = (string)r["UserName"], C = (int)r["Cnt"] })
                       .ToList();

        rows.Should().BeEquivalentTo(new[]
        {
            new { U = "george", C = 3 },
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


