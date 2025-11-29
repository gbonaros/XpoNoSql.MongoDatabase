using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class JoinsAndProjectionTests
{
    private readonly DbFixture _fx;
    public JoinsAndProjectionTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void ManyToMany_Junction_Projection_JoinUpdateReflects()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var admin = new AppRole(uow) { Name = "Admin" };
        var editor = new AppRole(uow) { Name = "Editor" };
        var geo = new AppUser(uow) { UserName = "george", IsActive = true };
        var maria = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();
        new UserRole(uow) { User = geo, Role = admin };
        new UserRole(uow) { User = geo, Role = editor };
        new UserRole(uow) { User = maria, Role = editor };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole))
        {
            Criteria = CriteriaOperator.Parse("User.IsActive = true")
        };
        view.Properties.AddRange(new[]
        {
            new ViewProperty("UserName", SortDirection.None, "User.UserName", false, true),
            new ViewProperty("RoleName", SortDirection.None, "Role.Name", false, true)
        });

        var rows = view.Cast<ViewRecord>().ToList();
        rows.Should().Contain(r => (string)r["UserName"] == "george" && (string)r["RoleName"] == "Admin");
        rows.Should().Contain(r => (string)r["UserName"] == "george" && (string)r["RoleName"] == "Editor");
        rows.Should().Contain(r => (string)r["UserName"] == "maria" && (string)r["RoleName"] == "Editor");

        editor.Name = "ContentEditor";
        uow.CommitChanges();
        view.Reload();
        var rows2 = view.Cast<ViewRecord>().ToList();
        rows2.Should().Contain(r => (string)r["UserName"] == "george" && (string)r["RoleName"] == "ContentEditor");
    }

    [Fact]
    public void MixedPrePostJoinCriteria_PartitionerCorrect()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var admin = new AppRole(uow) { Name = "Admin" };
        var editor = new AppRole(uow) { Name = "Editor" };
        var geo = new AppUser(uow) { UserName = "george", IsActive = true };
        var maria = new AppUser(uow) { UserName = "maria", IsActive = true };
        uow.CommitChanges();
        new UserRole(uow) { User = geo, Role = admin };
        new UserRole(uow) { User = geo, Role = editor };
        new UserRole(uow) { User = maria, Role = editor };
        uow.CommitChanges();

        var containsGe = new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty("User.UserName"), new OperandValue("ge"));
        var joinEqAdmin = new BinaryOperator(new OperandProperty("Role.Name"), new OperandValue("Admin"), BinaryOperatorType.Equal);
        var mixed = new GroupOperator(GroupOperatorType.And, containsGe, joinEqAdmin);

        var results = new XPCollection<UserRole>(uow, mixed).ToList();
        results.Should().Contain(ur => ur.User.UserName == "george" && ur.Role.Name == "Admin");
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<UserRole>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppUser>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<AppRole>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
