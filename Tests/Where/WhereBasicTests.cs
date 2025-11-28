
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Where;

[Collection(XpoCollection.Name)]
public class WhereBasicTests
{
    private readonly DbFixture _fx;

    public WhereBasicTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Where_On_Numeric_Range_And_Boolean()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Small", Value = 1, Price = 5m, IsActive = true };
        new SimpleItem(uow) { Name = "Medium", Value = 10, Price = 25m, IsActive = true };
        new SimpleItem(uow) { Name = "Inactive", Value = 8, Price = 15m, IsActive = false };
        uow.CommitChanges();

        var results = uow.Query<SimpleItem>()
            .Where(i => i.IsActive && i.Price >= 10m && i.Price <= 30m)
            .OrderBy(i => i.Price)
            .ToArray();

        results.Should().HaveCount(1);
        results.Single().Name.Should().Be("Medium");
    }

    [Fact]
    public void Where_On_Parent_Title_And_Child_Category()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "FilterParent", Notes = "Notes" };
        new SimpleChild(uow) { Label = "Match", Category = "Keep", Order = 1, IsDone = true, Parent = parent };
        new SimpleChild(uow) { Label = "Skip", Category = "Drop", Order = 2, IsDone = true, Parent = parent };
        uow.CommitChanges();

        var filtered = uow.Query<SimpleChild>()
            .Where(c => c.Parent != null && c.Parent.Title == "FilterParent" && c.Category == "Keep")
            .ToArray();

        filtered.Should().HaveCount(1);
        filtered.Single().Label.Should().Be("Match");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

