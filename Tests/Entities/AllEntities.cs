using System;
using DevExpress.Xpo;

// Legacy model entities used across tests (MongoProvider.Tests.Models)
namespace XpoNoSql.Tests
{
    public class Customer : XPBaseObject
    {
        [Key(true)] public Guid Key { get; set; }
        public Customer(Session session) : base(session) { }

        string fullname = string.Empty;

        [Size(SizeAttribute.DefaultStringMappingFieldSize)]
        public string Fullname
        {
            get => fullname;
            set => SetPropertyValue(nameof(Fullname), ref fullname, value);
        }

        [Aggregated, Association("Customer-Orders")]
        public XPCollection<Order> Orders => GetCollection<Order>(nameof(Orders));
    }

    public class Order : XPObject
    {
        public Order(Session session) : base(session) { }

        Customer customer;

        [Association("Customer-Orders")]
        public Customer Customer
        {
            get => customer;
            set => SetPropertyValue(nameof(Customer), ref customer, value);
        }

        [Aggregated, Association("Order-Products")]
        public XPCollection<Product> Products => GetCollection<Product>(nameof(Products));
    }

    public class Product : XPObject
    {
        public Product(Session session) : base(session) { }

        Order order;
        decimal price;
        string description = string.Empty;

        [Size(SizeAttribute.DefaultStringMappingFieldSize)]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value);
        }

        public decimal Price
        {
            get => price;
            set => SetPropertyValue(nameof(Price), ref price, value);
        }

        [Association("Order-Products")]
        public Order Order
        {
            get => order;
            set => SetPropertyValue(nameof(Order), ref order, value);
        }
    }

    [Persistent("Customers")]
    public sealed class TestCustomer : XPObject
    {
        public TestCustomer(Session session) : base(session) { }

        private string name = string.Empty;
        private string email = string.Empty;

        public string Name
        {
            get => name;
            set => SetPropertyValue(nameof(Name), ref name, value);
        }

        public string Email
        {
            get => email;
            set => SetPropertyValue(nameof(Email), ref email, value);
        }

        [Aggregated, Association("Customer-Orders")]
        public XPCollection<TestOrder> Orders => GetCollection<TestOrder>(nameof(Orders));
    }

    [Persistent("Orders")]
    public sealed class TestOrder : XPObject
    {
        public TestOrder(Session session) : base(session) { }

        private string productName = string.Empty;
        private int quantity;
        private decimal total;
        private TestCustomer? customer;

        public string ProductName
        {
            get => productName;
            set => SetPropertyValue(nameof(ProductName), ref productName, value);
        }

        public int Quantity
        {
            get => quantity;
            set => SetPropertyValue(nameof(Quantity), ref quantity, value);
        }

        public decimal Total
        {
            get => total;
            set => SetPropertyValue(nameof(Total), ref total, value);
        }

        [Association("Customer-Orders")]
        public TestCustomer? Customer
        {
            get => customer;
            set => SetPropertyValue(nameof(Customer), ref customer, value);
        }
    }

    public class NP3_PersonBase : XPObject
    {
        public NP3_PersonBase(Session s) : base(s) { }

        public string FullName { get; set; } = "";
        public decimal Salary { get; set; }
    }

    public class NP3_Employee : NP3_PersonBase
    {
        public NP3_Employee(Session s) : base(s) { }

        [Association("Manager-Employees")]
        public NP3_Manager Manager { get; set; }
    }

    public class NP3_Manager : NP3_PersonBase
    {
        public NP3_Manager(Session s) : base(s) { }

        [Association("Manager-Employees")]
        public XPCollection<NP3_Employee> Employees => GetCollection<NP3_Employee>(nameof(Employees));

        public decimal Bonus { get; set; }
    }

    [Persistent("SimpleItems")]
    public sealed class SimpleItem : XPObject
    {
        public SimpleItem(Session session) : base(session) { }

        private string name = string.Empty;
        private string description = string.Empty;
        private int value;
        private decimal price;
        private bool isActive;
        private DateTime createdOn = DateTime.UtcNow;

        public string Name
        {
            get => name;
            set => SetPropertyValue(nameof(Name), ref name, value);
        }

        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value);
        }

        public int Value
        {
            get => value;
            set => SetPropertyValue(nameof(Value), ref this.value, value);
        }

        public decimal Price
        {
            get => price;
            set => SetPropertyValue(nameof(Price), ref price, value);
        }

        public bool IsActive
        {
            get => isActive;
            set => SetPropertyValue(nameof(IsActive), ref isActive, value);
        }

        public DateTime CreatedOn
        {
            get => createdOn;
            set => SetPropertyValue(nameof(CreatedOn), ref createdOn, value);
        }
    }

    [Persistent("SimpleParents")]
    public sealed class SimpleParent : XPObject
    {
        public SimpleParent(Session session) : base(session) { }

        private string title = string.Empty;
        private string notes = string.Empty;
        private DateTime createdOn = DateTime.UtcNow;

        public string Title
        {
            get => title;
            set => SetPropertyValue(nameof(Title), ref title, value);
        }

        public string Notes
        {
            get => notes;
            set => SetPropertyValue(nameof(Notes), ref notes, value);
        }

        public DateTime CreatedOn
        {
            get => createdOn;
            set => SetPropertyValue(nameof(CreatedOn), ref createdOn, value);
        }

        [Association("Parent-Children"), Aggregated]
        public XPCollection<SimpleChild> Children => GetCollection<SimpleChild>(nameof(Children));
    }

    [Persistent("SimpleChildren")]
    public sealed class SimpleChild : XPObject
    {
        public SimpleChild(Session session) : base(session) { }

        private string label = string.Empty;
        private string category = string.Empty;
        private int order;
        private bool isDone;
        private SimpleParent? parent;

        public string Label
        {
            get => label;
            set => SetPropertyValue(nameof(Label), ref label, value);
        }

        public string Category
        {
            get => category;
            set => SetPropertyValue(nameof(Category), ref category, value);
        }

        public int Order
        {
            get => order;
            set => SetPropertyValue(nameof(Order), ref order, value);
        }

        public bool IsDone
        {
            get => isDone;
            set => SetPropertyValue(nameof(IsDone), ref isDone, value);
        }

        [Association("Parent-Children")]
        public SimpleParent? Parent
        {
            get => parent;
            set => SetPropertyValue(nameof(Parent), ref parent, value);
        }
    }
}
