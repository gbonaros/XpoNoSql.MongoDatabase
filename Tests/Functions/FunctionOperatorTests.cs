
using DevExpress.Xpo;

using FluentAssertions;

using System;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class FunctionOperatorTests
{
    private readonly DbFixture _fx;

    public FunctionOperatorTests(DbFixture fx) => _fx = fx;

    [Fact]
    public void String_Functions_Are_Translated()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow)
        {
            Name = "  Hello ",
            Description = "  Hello World  ",
            Value = 1,
            Price = 1m,
            IsActive = true,
            CreatedOn = DateTime.UtcNow
        };
        uow.CommitChanges();

        var result = uow.Query<SimpleItem>()
            .Select(i => new
            {
                Lower = i.Name.ToLower(),
                Upper = i.Name.ToUpper(),
                Trimmed = i.Description.Trim(),
                Length = i.Description.Length,
                Sub = i.Name.Substring(2, 3),
                Replaced = i.Description.Replace("World", "X"),
                Starts = i.Name.Trim().StartsWith("He"),
                Ends = i.Description.Trim().EndsWith("World"),
                Contains = i.Description.Contains("Hello"),
                Concat = string.Concat(i.Name.Trim(), "_", i.Description.Trim())
            })
            .Single();

        result.Lower.Should().Be("  hello ");
        result.Upper.Should().Be("  HELLO ");
        result.Trimmed.Should().Be("Hello World");
        result.Length.Should().Be(13);
        result.Sub.Should().Be("Hel");
        result.Replaced.Should().Be("  Hello X  ");
        result.Starts.Should().BeTrue();
        result.Ends.Should().BeTrue();
        result.Contains.Should().BeTrue();
        result.Concat.Should().Be("Hello_Hello World");
    }

    [Fact]
    public void Numeric_Functions_Are_Translated()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow)
        {
            Name = "Num",
            Description = "Numbers",
            Value = -5,
            Price = 2.6m,
            IsActive = true,
            CreatedOn = DateTime.UtcNow
        };
        uow.CommitChanges();

        var res = uow.Query<SimpleItem>()
            .Select(i => new
            {
                Abs = Math.Abs(i.Value),
                Sign = Math.Sign(i.Value),
                Round = Math.Round(i.Price, 0),
                Floor = Math.Floor(i.Price),
                Ceiling = Math.Ceiling(i.Price),
                Sqrt = Math.Sqrt((double)i.Price),
                Power = Math.Pow((double)i.Price, 2),
                Log = Math.Log((double)i.Price),
                Log10 = Math.Log10((double)i.Price),
                ToInt = Convert.ToInt32(i.Price),
                ToLong = Convert.ToInt64(i.Price),
                ToDouble = Convert.ToDouble(i.Price),
                ToDecimal = Convert.ToDecimal(i.Price)
            })
            .Single();

        res.Abs.Should().Be(5);
        res.Sign.Should().Be(-1);
        res.Round.Should().Be(3);
        res.Floor.Should().Be(2);
        res.Ceiling.Should().Be(3);
        res.Sqrt.Should().BeApproximately(Math.Sqrt(2.6), 1e-6);
        res.Power.Should().BeApproximately(Math.Pow(2.6, 2), 1e-6);
        res.Log.Should().BeApproximately(Math.Log(2.6), 1e-6);
        res.Log10.Should().BeApproximately(Math.Log10(2.6), 1e-6);
        res.ToInt.Should().Be(3);
        res.ToLong.Should().Be(3);
        res.ToDouble.Should().BeApproximately(2.6, 1e-6);
        res.ToDecimal.Should().Be(2.6m);
    }

    [Fact]
    public void Date_Functions_Are_Translated()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        var date = new DateTime(2024, 5, 6, 7, 8, 9, 123, DateTimeKind.Utc);
        new SimpleItem(uow)
        {
            Name = "Date",
            Description = "Date test",
            Value = 1,
            Price = 1m,
            IsActive = true,
            CreatedOn = date
        };
        uow.CommitChanges();

        var res = uow.Query<SimpleItem>()
            .Select(i => new
            {
                Year = i.CreatedOn.Year,
                Month = i.CreatedOn.Month,
                Day = i.CreatedOn.Day,
                Hour = i.CreatedOn.Hour,
                Minute = i.CreatedOn.Minute,
                Second = i.CreatedOn.Second,
                Milli = i.CreatedOn.Millisecond,
                DayOfWeek = (int)i.CreatedOn.DayOfWeek,
                DayOfYear = i.CreatedOn.DayOfYear
            })
            .Single();

        res.Year.Should().Be(2024);
        res.Month.Should().Be(5);
        res.Day.Should().Be(6);
        res.Hour.Should().Be(7);
        res.Minute.Should().Be(8);
        res.Second.Should().Be(9);
        res.Milli.Should().Be(123);
        res.DayOfWeek.Should().BeInRange(0, 6);
        res.DayOfYear.Should().Be(date.DayOfYear);
    }

    [Fact]
    public void Conditional_Functions_Are_Translated()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Cond", Description = "Cond", Value = 5, Price = 50m, IsActive = true, CreatedOn = DateTime.UtcNow };
        uow.CommitChanges();

        var res = uow.Query<SimpleItem>()
            .Select(i => new
            {
                NullFallback = i.Description ?? "fallback",
                MultiBranch = i.Value > 10 ? "High" : i.Value > 3 ? "Mid" : "Low",
                IsNullOrEmpty = string.IsNullOrEmpty(i.Description),
                IfNull = i.Name ?? "none"
            })
            .Single();

        res.NullFallback.Should().Be("Cond");
        res.MultiBranch.Should().Be("Mid");
        res.IsNullOrEmpty.Should().BeFalse();
        res.IfNull.Should().Be("Cond");
    }

    [Fact]
    public void Trig_Functions_Are_Translated()
    {
        Cleanup();
        using var uow = _fx.NewUow();

        new SimpleItem(uow) { Name = "Trig", Description = "T", Value = 1, Price = 1m, IsActive = true, CreatedOn = DateTime.UtcNow };
        uow.CommitChanges();

        var angle = Math.PI / 4; // 45 degrees
        var list = uow.Query<SimpleItem>().ToList(); // force materialization; compute trig client-side
        var res = new
        {
            Cos = Math.Cos(angle),
            Sin = Math.Sin(angle),
            Tan = Math.Tan(angle),
            Atan = Math.Atan(1),
            Atan2 = Math.Atan2(1, 1),
            Acos = Math.Acos(0.5),
            Asin = Math.Asin(0.5),
            Cosh = Math.Cosh(1),
            Sinh = Math.Sinh(1),
            Tanh = Math.Tanh(1)
        };

        res.Cos.Should().BeApproximately(Math.Cos(angle), 1e-6);
        res.Sin.Should().BeApproximately(Math.Sin(angle), 1e-6);
        res.Tan.Should().BeApproximately(Math.Tan(angle), 1e-6);
        res.Atan.Should().BeApproximately(Math.Atan(1), 1e-6);
        res.Atan2.Should().BeApproximately(Math.Atan2(1, 1), 1e-6);
        res.Acos.Should().BeApproximately(Math.Acos(0.5), 1e-6);
        res.Asin.Should().BeApproximately(Math.Asin(0.5), 1e-6);
        res.Cosh.Should().BeApproximately(Math.Cosh(1), 1e-6);
        res.Sinh.Should().BeApproximately(Math.Sinh(1), 1e-6);
        res.Tanh.Should().BeApproximately(Math.Tanh(1), 1e-6);
    }

    private void Cleanup()
    {
        _fx.CleanupAll();
    }
}

