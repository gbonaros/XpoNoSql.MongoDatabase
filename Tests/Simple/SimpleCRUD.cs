
using DevExpress.Xpo;

using FluentAssertions;

using System.Linq;



using Xunit;

namespace XpoNoSql.Tests;

[Collection(XpoCollection.Name)]
public class SimpleCRUD
{
    private readonly DbFixture _fx;

    public SimpleCRUD(DbFixture fx) => _fx = fx;

    [Fact]
    public void SimpleAddDelete()
    {
        using var uow = _fx.NewUow();
        var obj = new SimpleItem(uow) { Name = "Alpha", Value = 1 };
        uow.CommitChanges();

        obj.Delete();   
        uow.CommitChanges();
    }
}

