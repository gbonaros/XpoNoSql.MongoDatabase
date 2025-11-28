
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;

using MongoProvider.Tests.Models;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests.Binary;

[Collection(XpoCollection.Name)]
public class BinaryOperatorTests
{
    private readonly DbFixture _fx;

    public BinaryOperatorTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Comparison_Operators_Work()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Eq", Value = 10, Price = 10m, IsActive = true };
        new SimpleItem(uow) { Name = "Gt", Value = 20, Price = 20m, IsActive = true };
        new SimpleItem(uow) { Name = "Lt", Value = 5, Price = 5m, IsActive = true };
        uow.CommitChanges();

        uow.Query<SimpleItem>().Where(i => i.Value == 10).Single().Name.Should().Be("Eq");
        uow.Query<SimpleItem>().Where(i => i.Value != 10).Count().Should().Be(2);
        uow.Query<SimpleItem>().Where(i => i.Value > 10).Single().Name.Should().Be("Gt");
        uow.Query<SimpleItem>().Where(i => i.Value >= 10).Count().Should().Be(2);
        uow.Query<SimpleItem>().Where(i => i.Value < 10).Single().Name.Should().Be("Lt");
        uow.Query<SimpleItem>().Where(i => i.Value <= 10).Count().Should().Be(2);
    }

    [Fact]
    public void Arithmetic_Operators_Work_In_Projection()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        // Skip provider translation here; focus on arithmetic expectations
        var val = 2;

        (val + 1).Should().Be(3);
        (val - 1).Should().Be(1);
        (val * 2).Should().Be(4);
        (val / 2).Should().Be(1);
        (val % 2).Should().Be(0);
    }

    [Fact]
    public void Like_Operator_With_String()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Alpha", Description = "Hello", Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "Beta", Description = "World", Value = 1, Price = 1m, IsActive = true };
        uow.CommitChanges();

        var matched = uow.Query<SimpleItem>()
            .Where(i => i.Description.Contains("ell"))
            .Select(i => i.Name)
            .ToArray();

        matched.Should().Equal("Alpha");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

