using System.Linq;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using FluentAssertions;
using Xunit;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

[Collection(XpoCollection.Name)]
public class LikeOperator_Tests
{
    private readonly DbFixture _fx;
    public LikeOperator_Tests(DbFixture fx) => _fx = fx;

    [Fact]
    public void Customer_Name_Like_Patterns_With_Criteria()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice" };
            var c2 = new TestCustomer(uow) { Name = "Alex" };
            var c3 = new TestCustomer(uow) { Name = "Bob" };
            var c4 = new TestCustomer(uow) { Name = "Charlie" };
            uow.CommitChanges();

            // ---------------------------------------------------
            // 1) Name Like 'A%'  -> Alice, Alex
            // ---------------------------------------------------
            var likeA = new XPCollection<TestCustomer>(
                uow,
                CriteriaOperator.Parse("Name Like ?", "A%"))
                .OrderBy(c => c.Name)
                .ToArray();

            likeA.Select(c => c.Name)
                 .Should()
                 .BeEquivalentTo(new[] { "Alice", "Alex" });

            // ---------------------------------------------------
            // 2) Name Like '%e'  -> Alice, Charlie
            // ---------------------------------------------------
            var endsWithE = new XPCollection<TestCustomer>(
                uow,
                CriteriaOperator.Parse("Name Like ?", "%e"))
                .OrderBy(c => c.Name)
                .ToArray();

            endsWithE.Select(c => c.Name)
                     .Should()
                     .BeEquivalentTo(new[] { "Alice", "Charlie" });

            // ---------------------------------------------------
            // 3) Name Like '%l%' -> Alice, Charlie
            // ---------------------------------------------------
            var containsL = new XPCollection<TestCustomer>(
                uow,
                CriteriaOperator.Parse("Name Like ?", "%l%"))
                .OrderBy(c => c.Name)
                .ToArray();

            containsL.Select(c => c.Name)
                     .Should()
                     .BeEquivalentTo(new[] { "Alex", "Alice", "Charlie" });

            // ---------------------------------------------------
            // 4) Case-sensitivity check via XpoDefault.DefaultCaseSensitive
            // ---------------------------------------------------
            var original = XpoDefault.DefaultCaseSensitive;

            try
            {
                // CASE INSENSITIVE
                XpoDefault.DefaultCaseSensitive = false;

                var insensitive = new XPCollection<TestCustomer>(
                        uow,
                        CriteriaOperator.Parse("Name Like ?", "a%"))
                    .Select(c => c.Name)
                    .ToArray();

                insensitive.Should().Contain(new[] { "Alice", "Alex" });

                // CASE SENSITIVE
                XpoDefault.DefaultCaseSensitive = true;

                var sensitive = new XPCollection<TestCustomer>(
                        uow,
                        CriteriaOperator.Parse("Name Like ?", "a%"))
                    .Select(c => c.Name)
                    .ToArray();

                sensitive.Should().BeEmpty();
            }
            finally
            {
                XpoDefault.DefaultCaseSensitive = original;
            }
        }
    }


    [Fact]
    public void Customer_Name_Like_Patterns_With_BinaryOperator()
    {
        using (UnitOfWork uow = _fx.NewUow())
        {
            Cleanup(uow);

            // -----------------------------
            // Test data
            // -----------------------------
            var c1 = new TestCustomer(uow) { Name = "Alice" };
            var c2 = new TestCustomer(uow) { Name = "Alex" };
            var c3 = new TestCustomer(uow) { Name = "Bob" };
            var c4 = new TestCustomer(uow) { Name = "Charlie" };
            uow.CommitChanges();

            // ---------------------------------------------------
            // 1) Name Like 'A%'  -> Alice, Alex
            // ---------------------------------------------------
            var likeA = new XPCollection<TestCustomer>(
                uow,
                new BinaryOperator(nameof(TestCustomer.Name), "A%", BinaryOperatorType.Like))
                .OrderBy(c => c.Name)
                .ToArray();

            likeA.Select(c => c.Name)
                 .Should()
                 .BeEquivalentTo(new[] { "Alice", "Alex" });

            // ---------------------------------------------------
            // 2) Name Like '%e'  -> Alice, Charlie
            // ---------------------------------------------------
            var endsWithE = new XPCollection<TestCustomer>(
                uow,
                new BinaryOperator(nameof(TestCustomer.Name), "%e", BinaryOperatorType.Like))
                .OrderBy(c => c.Name)
                .ToArray();

            endsWithE.Select(c => c.Name)
                     .Should()
                     .BeEquivalentTo(new[] { "Alice", "Charlie" });

            // ---------------------------------------------------
            // 3) Name Like '%l%' -> Alice, Charlie
            // ---------------------------------------------------
            var containsL = new XPCollection<TestCustomer>(
                uow,
                new BinaryOperator(nameof(TestCustomer.Name), "%l%", BinaryOperatorType.Like))
                .OrderBy(c => c.Name)
                .ToArray();

            containsL.Select(c => c.Name)
                     .Should()
                     .BeEquivalentTo(new[] { "Alex", "Alice", "Charlie" });

            // ---------------------------------------------------
            // 4) Case-sensitivity check via XpoDefault.DefaultCaseSensitive
            // ---------------------------------------------------
            var original = XpoDefault.DefaultCaseSensitive;

            try
            {
                // CASE INSENSITIVE
                XpoDefault.DefaultCaseSensitive = false;

                var insensitive = new XPCollection<TestCustomer>(
                        uow,
                        new BinaryOperator(nameof(TestCustomer.Name), "a%", BinaryOperatorType.Like))
                    .Select(c => c.Name)
                    .ToArray();

                insensitive.Should().Contain(new[] { "Alice", "Alex" });

                // CASE SENSITIVE
                XpoDefault.DefaultCaseSensitive = true;

                var sensitive = new XPCollection<TestCustomer>(
                        uow,
                        new BinaryOperator(nameof(TestCustomer.Name), "a%", BinaryOperatorType.Like))
                    .Select(c => c.Name)
                    .ToArray();

                sensitive.Should().BeEmpty();
            }
            finally
            {
                XpoDefault.DefaultCaseSensitive = original;
            }
        }
    }

    private static void Cleanup(UnitOfWork uow)
    {
        foreach (var obj in new XPCollection<TestCustomer>(uow).ToList()) obj.Delete();
        foreach (var obj in new XPCollection<TestOrder>(uow).ToList()) obj.Delete();
        uow.CommitChanges();
    }
}
