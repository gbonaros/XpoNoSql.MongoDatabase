using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class LinqQuerySimpleAndJoinTests
{
    private readonly DbFixture _fx;
    public LinqQuerySimpleAndJoinTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Linq_Simple_Where_Order_Take()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Person(uow) { Name = "Gamma" };
        new Person(uow) { Name = "Alpha" };
        new Person(uow) { Name = "Delta" };
        new Person(uow) { Name = "Beta" };
        uow.CommitChanges();

        var q = new XPQuery<Person>(uow)
            .Where(p => p.Name.Contains("a"))   // case-insensitive contains
            .OrderBy(p => p.Name)
            .Take(2)
            .Select(p => p.Name)
            .ToList();

        q.Should().Equal(new[] { "Alpha", "Beta" });
    }

    //[Fact]
    //public void Linq_Join_User_Role_Projection()
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

    //    // Active users that are Admin OR Editor, project (user, role)
    //    var ur = new XPQuery<UserRole>(uow);
    //    var rows = ur
    //        .Where(lnk => lnk.User.IsActive && (lnk.Role.Name == "Admin" || lnk.Role.Name == "Editor"))
    //        .OrderBy(lnk => lnk.User.UserName)
    //        .ThenBy(lnk => lnk.Role.Name)
    //        .Select(lnk => new { lnk.User.UserName, Role = lnk.Role.Name })
    //        .ToList();

    //    rows.Should().ContainSingle(x => x.UserName == "george" && x.Role == "Admin");
    //    rows.Should().ContainSingle(x => x.UserName == "george" && x.Role == "Editor");
    //    rows.Should().ContainSingle(x => x.UserName == "maria" && x.Role == "Editor");
    //    rows.Should().NotContain(x => x.UserName == "x");
    //}

    //[Fact]
    //public void Linq_GroupBy_Count_Roles_Per_User()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    var admin = new AppRole(uow) { Name = "Admin" };
    //    var editor = new AppRole(uow) { Name = "Editor" };
    //    var dev = new AppRole(uow) { Name = "Dev" };
    //    var g1 = new AppUser(uow) { UserName = "george", IsActive = true };
    //    var m1 = new AppUser(uow) { UserName = "maria", IsActive = true };
    //    uow.CommitChanges();

    //    new UserRole(uow) { User = g1, Role = admin };
    //    new UserRole(uow) { User = g1, Role = editor };
    //    new UserRole(uow) { User = m1, Role = editor };
    //    new UserRole(uow) { User = m1, Role = dev };
    //    uow.CommitChanges();

    //    var ur = new XPQuery<UserRole>(uow);
    //    var counts = ur
    //        .GroupBy(lnk => lnk.User.UserName)
    //        .Select(g => new { UserName = g.Key, Roles = g.Count() })
    //        .OrderBy(x => x.UserName)
    //        .ToList();

    //    counts.Should().BeEquivalentTo(new[]
    //    {
    //        new { UserName = "george", Roles = 2 },
    //        new { UserName = "maria",  Roles = 2 }
    //    });
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
