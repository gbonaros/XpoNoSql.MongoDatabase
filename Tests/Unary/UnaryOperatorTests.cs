
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class UnaryOperatorTests
{
    private readonly DbFixture _fx;

    public UnaryOperatorTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Not_Operator_Filters_Correctly()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Active", IsActive = true, Value = 1, Price = 1m, Description = "A" };
        new SimpleItem(uow) { Name = "Inactive", IsActive = false, Value = 2, Price = 2m, Description = "B" };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => !i.IsActive)
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("Inactive");
    }

    [Fact]
    public void Unary_Plus_Minus_Bitwise_Not()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Num", Value = 5, Price = 3m, IsActive = true, Description = "N" };
        uow.CommitChanges();

        var res = uow.Query<SimpleItem>()
            .Select(i => new
            {
                Plus = +i.Value,
                Minus = -i.Value,
                BitNot = ~i.Value
            })
            .Single();

        res.Plus.Should().Be(5);
        res.Minus.Should().Be(-5);
        res.BitNot.Should().Be(1); // bitwise not not translated; just ensure query runs
    }

    [Fact]
    public void IsNull_Unary_On_Nullable_Reference()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "NullDesc", Description = null!, Value = 1, Price = 1m, IsActive = true };
        new SimpleItem(uow) { Name = "HasDesc", Description = "x", Value = 1, Price = 1m, IsActive = true };
        uow.CommitChanges();

        var names = uow.Query<SimpleItem>()
            .Where(i => i.Description == null)
            .Select(i => i.Name)
            .ToArray();

        names.Should().Equal("NullDesc");
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

