# XpoNoSql.MongoDatabase

A custom **MongoDB-backed XPO DataStore** for DevExpress XPO  
(`IDataStore`, `SimpleDataLayer`, `ThreadSafeDataLayer` compatible).

This provider translates **XPO SelectStatements, ModificationStatements, CriteriaOperator trees, joins, aggregates, and subqueries** into MongoDB aggregation pipelines — allowing XPO to run seamlessly on MongoDB.

---

## ✨ Features

- Full XPO `SelectStatement` → MongoDB `$match`, `$lookup`, `$group`, `$project`, `$sort`
- CriteriaOperator support:
  - Binary operators, group operators, function operators
  - LIKE with SQL-style `%` / `_` semantics (converted to regex)
  - StartsWith / EndsWith / Contains
  - Case sensitivity respected via `XpoDefault.DefaultCaseSensitive`
- Joins:
  - Inner joins
  - Left Outer joins
  - Self-joins
- Aggregates:
  - Count, Sum, Avg, Min, Max, Exists
- Subqueries (inside WHERE / HAVING / projections)
- Materialization to `SelectStatementResult` and `XPView`
- CRUD:
  - Insert / Update / Delete with optimistic locking
  - Identity generation via atomic counter collection

---

## 🚀 Quick Start

Minimal setup to run XPO against MongoDB:

```csharp
using DevExpress.Xpo;
using XpoNoSql.MongoDatabase;

// Register the provider so XPO can resolve the "MongoDB" connection string scheme.
MongoConnectionProvider.Register();

// Build the MongoDB XPO connection string.
string connectionString =
    MongoConnectionProvider.GetConnectionString(
        connectionUri: "mongodb://localhost:27017",
        databaseName: "XpoMongoDemo");

// Create the XPO IDataStore using the built-in DevExpress factory.
IDataStore provider =
    XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);

// Assign the application's data layer.
XpoDefault.DataLayer = new SimpleDataLayer(provider);

// Use XPO as usual.
using (var uow = new UnitOfWork())
{
    var customer = new TestCustomer(uow) { Name = "Alice" };
    uow.CommitChanges();
}
```
No extra plumbing required.
All XPO operations — queries, joins, grouping, aggregates, 
and CRUD — automatically run through your MongoDB-backed provider.

## 🚧 Current Limits / Work in Progress
No standalone transaction support (Mongo does not require explicit Begin/Commit in this provider)
Some advanced XPO functions still unimplemented
Update/Delete with subqueries may not be fully supported
Multi-database support planned
Pull requests welcome 🚀

## 📦 Installation
clone the repository:
```bash
git clone https://github.com/gbonaros/XpoNoSql.MongoDatabase.git
```
or install from NuGet
```bash
dotnet add package XpoNoSql.MongoDatabase
