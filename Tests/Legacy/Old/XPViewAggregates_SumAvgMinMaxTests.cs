using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using Xunit;

namespace XpoNoSql.Tests;
[Collection(XpoCollection.Name)]
public class XPViewAggregates_SumAvgMinMaxTests
{
    private readonly DbFixture _fx;
    public XPViewAggregates_SumAvgMinMaxTests(DbFixture fx) => _fx = fx;

    //[Fact]
    //public void XPView_GroupBy_Sum_PerKey()
    //{
    //    using var uow = _fx.NewUow();
    //    Cleanup(uow);

    //    // A as key, sum B
    //    new Metric(uow) { A = 1, B = 10 };
    //    new Metric(uow) { A = 1, B = 5 };
    //    new Metric(uow) { A = 2, B = 7 };
    //    uow.CommitChanges();

    //    var view = new XPView(uow, typeof(Metric));
    //    view.Properties.Add(new ViewProperty("KeyA", SortDirection.Ascending, "A", true, true));     // group by A
    //    view.Properties.Add(new ViewProperty("SumB", SortDirection.None, "Sum([B])", false, true)); // sum

    //    var rows = view.Cast<ViewRecord>()
    //        .Select(r => new { A = (int)r["KeyA"], S = (int)r["SumB"] })
    //        .OrderBy(x => x.A)
    //        .ToList();

    //    rows.Should().BeEquivalentTo(new[] {
    //        new { A = 1, S = 15 }, new { A = 2, S = 7 }
    //    });
    //}

    [Fact]
    public void XPView_Global_Avg_Min_Max_NoGroup()
    {
        using var uow = _fx.NewUow();
        Cleanup(uow);

        new Metric(uow) { A = 12, B = 5 };
        new Metric(uow) { A = 23, B = 15 };
        new Metric(uow) { A = 38, B = 25 };
        uow.CommitChanges();

        var view = new XPView(uow, typeof(Metric));
        view.Properties.Add(new ViewProperty("AvgA", SortDirection.None, "Avg([A])", false, true));
        view.Properties.Add(new ViewProperty("MinB", SortDirection.None, "Min([B])", false, true));
        view.Properties.Add(new ViewProperty("MaxB", SortDirection.None, "Max([B])", false, true));

        var rec = view.Cast<ViewRecord>().Single();
        Convert.ToDouble(rec["AvgA"]).Should().BeApproximately(24, 0.1);
        ((int)rec["MinB"]).Should().Be(5);
        ((int)rec["MaxB"]).Should().Be(25);
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
