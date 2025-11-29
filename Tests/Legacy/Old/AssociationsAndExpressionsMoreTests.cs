using DevExpress.Data.Filtering;
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;


[Collection(XpoCollection.Name)]
public class AssociationsAndExpressionsMoreTests
{
    private readonly DbFixture _fx;
    public AssociationsAndExpressionsMoreTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Junction_Delete_ReflectsInProjection()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var admin = new AppRole(uow) { Name = "Admin" };
        var editor = new AppRole(uow) { Name = "Editor" };
        var geo = new AppUser(uow) { UserName = "george", IsActive = true };
        uow.CommitChanges();

        var ur1 = new UserRole(uow) { User = geo, Role = admin };
        var ur2 = new UserRole(uow) { User = geo, Role = editor };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(UserRole));
        view.Properties.Add(new ViewProperty("UserName", SortDirection.None, "User.UserName", false, true));
        view.Properties.Add(new ViewProperty("RoleName", SortDirection.None, "Role.Name", false, true));

        var before = view.Cast<ViewRecord>().Where(r => (string)r["UserName"] == "george").ToList();
        before.Select(r => (string)r["RoleName"]).Should().BeEquivalentTo(new[] { "Admin", "Editor" });

        // Delete one link
        ur2.Delete();
        uow.CommitChanges();

        view.Reload();
        var after = view.Cast<ViewRecord>().Where(r => (string)r["UserName"] == "george").ToList();
        after.Select(r => (string)r["RoleName"]).Should().BeEquivalentTo(new[] { "Admin" });
    }

    [Fact]
    public void Reassign_Child_Association_MovesCorrectly()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var p1 = new Person(uow) { Name = "P1" };
        var p2 = new Person(uow) { Name = "P2" };
        var k = new Kid(uow) { KidName = "K", Parent = p1 };
        uow.CommitChanges();

        uow.Reload(p1);
        p1.Kids.Count.Should().Be(1);
        p2.Kids.Count.Should().Be(0);

        // Move kid to other parent
        k.Parent = p2;
        uow.CommitChanges();

        uow.Reload(p1); uow.Reload(p2);
        p1.Kids.Count.Should().Be(0);
        p2.Kids.Count.Should().Be(1);
        p2.Kids[0].KidName.Should().Be("K");
    }

    [Fact]
    public void Arithmetic_Subtract_Divide_Modulo()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var m1 = new Metric(uow) { A = 10, B = 5, Flags = 3 }; // A-B=5, A/B=2, A%3=1
        var m2 = new Metric(uow) { A = 9, B = 3, Flags = 12 }; // A-B=6, A/B=3, A%3=0
        var m3 = new Metric(uow) { A = 4, B = 2, Flags = 8 }; // A-B=2, A/B=2, A%3=1
        uow.CommitChanges();

        // (A - B) >= 5 -> m1 (5), m2 (6)
        var subCrit = new BinaryOperator(
            new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Minus),
            new OperandValue(5),
            BinaryOperatorType.GreaterOrEqual);
        new XPCollection<Metric>(uow, subCrit).Select(m => m.Oid).Should().BeEquivalentTo(new[] { m1.Oid, m2.Oid });

        // (A / B) = 2 -> m1 (2), m3 (2)
        var divCrit = new BinaryOperator(
            new BinaryOperator(new OperandProperty("A"), new OperandProperty("B"), BinaryOperatorType.Divide),
            new OperandValue(2),
            BinaryOperatorType.Equal);
        new XPCollection<Metric>(uow, divCrit).Select(m => m.Oid).Should().BeEquivalentTo(new[] { m1.Oid, m3.Oid });

        // (A % 3) = 0 -> m2 only
        var modCrit = new BinaryOperator(
            new BinaryOperator(new OperandProperty("A"), new OperandProperty("3"), BinaryOperatorType.Modulo),
            new OperandValue(0),
            BinaryOperatorType.Equal);
        // Note: DevExpress Criteria doesn't support numeric literals as OperandProperty,
        // so we’ll use Parse for modulo:
        var modParsed = CriteriaOperator.Parse("[A] % 3 = 0");
        new XPCollection<Metric>(uow, modParsed).Select(m => m.Oid).Should().BeEquivalentTo(new[] { m2.Oid });
    }

    [Fact]
    public void Bitwise_FilterEvenOdd_WithModuloExpr()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        var m1 = new Metric(uow) { A = 2, B = 0, Flags = 1 };
        var m2 = new Metric(uow) { A = 3, B = 0, Flags = 2 };
        var m3 = new Metric(uow) { A = 10, B = 0, Flags = 3 };
        uow.CommitChanges();

        var evens = new XPCollection<Metric>(uow, CriteriaOperator.Parse("[A] % 2 = 0")).ToList();
        evens.Should().OnlyContain(m => (m.A % 2) == 0);

        var odds = new XPCollection<Metric>(uow, CriteriaOperator.Parse("[A] % 2 <> 0")).ToList();
        odds.Should().OnlyContain(m => (m.A % 2) != 0);
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
