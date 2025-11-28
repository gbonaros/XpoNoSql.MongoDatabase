
using DevExpress.Xpo;

using System;
using System.Linq;

namespace XpoNoSQL.MongoDatabase.Core.Tests;

// Minimal persistent classes used by the tests (copied from your smoke)
[Persistent]
public sealed class Person : XPBaseObject
{
    public Person(Session s) : base(s) { }
    [Key(true)] public Guid TestKey { get; set; }
    string name;
    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Name { get => name; set => SetPropertyValue(nameof(Name), ref name, value); }
    [Association("Person-Kids")]
    public XPCollection<Kid> Kids => GetCollection<Kid>(nameof(Kids));
}

[Persistent]
public sealed class Kid : XPBaseObject
{
    public Kid(Session s) : base(s) { }
    [Key(true)] public int Oid { get; set; }
    string kidName;
    [Size(128)]
    public string KidName { get => kidName; set => SetPropertyValue(nameof(KidName), ref kidName, value); }
    Person parent;
    [Association("Person-Kids")]
    public Person Parent { get => parent; set => SetPropertyValue(nameof(Parent), ref parent, value); }
}

[Persistent]
[DeferredDeletion(false)]
[OptimisticLocking(false)]
public sealed class AppUser : XPBaseObject
{
    public AppUser(Session s) : base(s) { }
    [Key(true)] public Guid Oid { get; set; }
    string userName;
    public string UserName { get => userName; set => SetPropertyValue(nameof(UserName), ref userName, value); }
    bool isActive;
    public bool IsActive { get => isActive; set => SetPropertyValue(nameof(IsActive), ref isActive, value); }
    [Association("User-UserRoles")]
    public XPCollection<UserRole> RolesLink => GetCollection<UserRole>(nameof(RolesLink));
}

[Persistent]
public sealed class AppRole : XPBaseObject
{
    public AppRole(Session s) : base(s) { }
    [Key(true)] public int Oid { get; set; }
    string name;
    public string Name { get => name; set => SetPropertyValue(nameof(Name), ref name, value); }
    [Association("Role-UserRoles")]
    public XPCollection<UserRole> UsersLink => GetCollection<UserRole>(nameof(UsersLink));
}

[Persistent]
public sealed class UserRole : XPBaseObject
{
    public UserRole(Session s) : base(s) { }
    [Key(true)] public int Oid { get; set; }
    AppUser user;
    [Association("User-UserRoles")]
    public AppUser User { get => user; set => SetPropertyValue(nameof(User), ref user, value); }
    AppRole role;
    [Association("Role-UserRoles")]
    public AppRole Role { get => role; set => SetPropertyValue(nameof(Role), ref role, value); }
}

[Persistent]
public sealed class Metric : XPBaseObject
{
    public Metric(Session s) : base(s) { }
    [Key(true)] public int Oid { get; set; }
    int a;
    public int A { get => a; set => SetPropertyValue(nameof(A), ref a, value); }
    int b;
    public int B { get => b; set => SetPropertyValue(nameof(B), ref b, value); }
    int flags;
    public int Flags { get => flags; set => SetPropertyValue(nameof(Flags), ref flags, value); }
}
