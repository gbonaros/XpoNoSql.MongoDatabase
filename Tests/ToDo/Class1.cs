using DevExpress.Xpo;

using FluentAssertions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace XpoNoSql.Tests
{
    [Collection(XpoCollection.Name)]
    public class ToDo1
    {
        private readonly DbFixture _fx;

        public ToDo1(DbFixture fx) => _fx = fx;

        [Fact]
        public void GuidKey()
        {
            _fx.CleanupAll();
            using var uow = _fx.NewUow();

            var obj = new CustomKeyObject(uow);
            obj.Property = "Test"; 
            obj.Price = 10;
            uow.CommitChanges();


            var result = uow.Query<CustomKeyObject>().ToArray();
            obj.Property.Should().Be("Test");
            obj.Price.Should().Be(10);
        }
    }


    public class CustomKeyObject : XPBaseObject
    {
        public CustomKeyObject(Session session) : base(session)
        { }


        decimal price;
        string property;
        Guid key;

        [Key(true)]
        public Guid Key
        {
            get => key;
            set => SetPropertyValue(nameof(Key), ref key, value);
        }


        [Size(SizeAttribute.DefaultStringMappingFieldSize)]
        public string Property
        {
            get => property;
            set => SetPropertyValue(nameof(Property), ref property, value);
        }

        public decimal Price
        {
            get => price;
            set => SetPropertyValue(nameof(Price), ref price, value);
        }
    }
}
