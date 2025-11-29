using DevExpress.Xpo;

using System;

namespace XpoNoSql.Tests;

[Persistent("ErpCustomers")]
public sealed class ErpCustomer : XPObject
{
    public ErpCustomer(Session session) : base(session)
    {
    }

    private string name = string.Empty;
    private string email = string.Empty;
    private string region = string.Empty;
    private string status = "Active";
    private DateTime createdOn = DateTime.UtcNow;
    private decimal creditLimit;

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

    public string Region
    {
        get => region;
        set => SetPropertyValue(nameof(Region), ref region, value);
    }

    public string Status
    {
        get => status;
        set => SetPropertyValue(nameof(Status), ref status, value);
    }

    public DateTime CreatedOn
    {
        get => createdOn;
        set => SetPropertyValue(nameof(CreatedOn), ref createdOn, value);
    }

    public decimal CreditLimit
    {
        get => creditLimit;
        set => SetPropertyValue(nameof(CreditLimit), ref creditLimit, value);
    }

    [Association("Customer-Orders"), Aggregated]
    public XPCollection<ErpOrder> Orders => GetCollection<ErpOrder>(nameof(Orders));
}

[Persistent("ErpProducts")]
public sealed class ErpProduct : XPObject
{
    public ErpProduct(Session session) : base(session)
    {
    }

    private string name = string.Empty;
    private string category = string.Empty;
    private decimal unitPrice;
    private bool isActive = true;

    public string Name
    {
        get => name;
        set => SetPropertyValue(nameof(Name), ref name, value);
    }

    public string Category
    {
        get => category;
        set => SetPropertyValue(nameof(Category), ref category, value);
    }

    public decimal UnitPrice
    {
        get => unitPrice;
        set => SetPropertyValue(nameof(UnitPrice), ref unitPrice, value);
    }

    public bool IsActive
    {
        get => isActive;
        set => SetPropertyValue(nameof(IsActive), ref isActive, value);
    }
}

[Persistent("ErpOrders")]
public sealed class ErpOrder : XPObject
{
    public ErpOrder(Session session) : base(session)
    {
    }

    private string orderNumber = string.Empty;
    private DateTime orderDate = DateTime.UtcNow;
    private string status = "Open";
    private decimal totalAmount;
    private ErpCustomer? customer;

    public string OrderNumber
    {
        get => orderNumber;
        set => SetPropertyValue(nameof(OrderNumber), ref orderNumber, value);
    }

    public DateTime OrderDate
    {
        get => orderDate;
        set => SetPropertyValue(nameof(OrderDate), ref orderDate, value);
    }

    public string Status
    {
        get => status;
        set => SetPropertyValue(nameof(Status), ref status, value);
    }

    public decimal TotalAmount
    {
        get => totalAmount;
        set => SetPropertyValue(nameof(TotalAmount), ref totalAmount, value);
    }

    [Association("Customer-Orders")]
    public ErpCustomer? Customer
    {
        get => customer;
        set => SetPropertyValue(nameof(Customer), ref customer, value);
    }

    [Association("Order-Lines"), Aggregated]
    public XPCollection<ErpOrderLine> Lines => GetCollection<ErpOrderLine>(nameof(Lines));
}

[Persistent("ErpOrderLines")]
public sealed class ErpOrderLine : XPObject
{
    public ErpOrderLine(Session session) : base(session)
    {
    }

    private int lineNumber;
    private int quantity;
    private decimal lineTotal;
    private ErpOrder? order;
    private ErpProduct? product;

    public int LineNumber
    {
        get => lineNumber;
        set => SetPropertyValue(nameof(LineNumber), ref lineNumber, value);
    }

    public int Quantity
    {
        get => quantity;
        set => SetPropertyValue(nameof(Quantity), ref quantity, value);
    }

    public decimal LineTotal
    {
        get => lineTotal;
        set => SetPropertyValue(nameof(LineTotal), ref lineTotal, value);
    }

    [Association("Order-Lines")]
    public ErpOrder? Order
    {
        get => order;
        set => SetPropertyValue(nameof(Order), ref order, value);
    }

    public ErpProduct? Product
    {
        get => product;
        set => SetPropertyValue(nameof(Product), ref product, value);
    }
}
