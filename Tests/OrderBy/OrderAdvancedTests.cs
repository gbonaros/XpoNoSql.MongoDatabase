
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.OrderBy;

[Collection(XpoCollection.Name)]
public class OrderAdvancedTests
{
    private readonly DbFixture _fx;

    public OrderAdvancedTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Order_With_Filter_And_Projection()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Low", Value = 1, Price = 5m, IsActive = true };
        new SimpleItem(uow) { Name = "High", Value = 2, Price = 50m, IsActive = true };
        new SimpleItem(uow) { Name = "Mid", Value = 3, Price = 25m, IsActive = false };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleItem>()
            .Where(i => i.IsActive)
            .Select(i => new { i.Name, i.Price })
            .OrderByDescending(i => i.Price)
            .ToArray();

        ordered.Should().HaveCount(2);
        ordered[0].Name.Should().Be("High");
        ordered[1].Name.Should().Be("Low");
    }

    [Fact]
    public void Order_By_Child_Order_Then_Label()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var parent = new SimpleParent(uow) { Title = "P", Notes = "Parent" };
        new SimpleChild(uow) { Label = "B", Category = "Cat", Order = 2, IsDone = true, Parent = parent };
        new SimpleChild(uow) { Label = "A", Category = "Cat", Order = 1, IsDone = true, Parent = parent };
        new SimpleChild(uow) { Label = "C", Category = "Cat", Order = 2, IsDone = true, Parent = parent };
        uow.CommitChanges();

        var ordered = uow.Query<SimpleChild>()
            .Where(c => c.Parent != null && c.Category == "Cat")
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Label)
            .Select(c => c.Label)
            .ToArray();

        ordered.Should().Equal("A", "B", "C");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

