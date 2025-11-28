using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;
[Collection(XpoCollection.Name)]
public class Sort_FunctionOperator_Projected_Tests
{
    private readonly DbFixture _fx;
    public Sort_FunctionOperator_Projected_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void NonGrouped_Sort_By_FunctionOperator_Projected()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new AppUser(uow) { UserName = "beta" };
        new AppUser(uow) { UserName = "Alpha" };
        new AppUser(uow) { UserName = "gamma" };
        uow.CommitChanges();

        // SELECT Upper([UserName]) as UP, UserName; ORDER BY Upper([UserName])
        var xp = new XPView(uow, typeof(AppUser));
        xp.Properties.Add(new ViewProperty("UP", SortDirection.None, "Upper([UserName])", false, true));
        xp.Properties.Add(new ViewProperty("UN", SortDirection.None, "UserName", false, true));


        xp.Sorting.Add(new SortProperty(
            new OperandProperty("UP"),
            SortingDirection.Ascending));

        var order = xp.Cast<ViewRecord>().Select(r => (string)r["UN"]).ToList();
        order.Should().Equal("Alpha", "beta", "gamma");
    }

    [Fact]
    public void Grouped_Sort_By_Count_Then_Name()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var r = new AppRole(uow) { Name = "R" };
        var beta = new AppUser(uow) { UserName = "beta" };
        var alpha = new AppUser(uow) { UserName = "alpha" };
        uow.CommitChanges();

        new UserRole(uow) { User = beta, Role = r };
        new UserRole(uow) { User = beta, Role = r };
        new UserRole(uow) { User = alpha, Role = r };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        // properties
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", true, true));
        view.Properties.Add(new ViewProperty("Cnt", SortDirection.None, "Count()", false, true));

        // ORDER BY Count() DESC, UserName ASC  -> use aliases "Cnt" and "UserName"
        view.Sorting.Add(new SortProperty(new OperandProperty("Cnt"), SortingDirection.Descending));
        view.Sorting.Add(new SortProperty(new OperandProperty("UserName"), SortingDirection.Ascending));


        var rows = view.Cast<ViewRecord>().Select(r => new { U = (string)r["UserName"], C = (int)r["Cnt"] }).ToList();

        rows.Should().BeEquivalentTo(new[]
        {
            new { U = "beta",  C = 2 },
            new { U = "alpha", C = 1 },
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
