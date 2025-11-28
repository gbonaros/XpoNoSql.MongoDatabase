using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Exceptions;

using XpoNoSQL.MongoDatabase.Core;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests;

public sealed class DbFixture : IAsyncLifetime
{
    private const string DefaultDatabaseName = "xpo-mongo-crud-tests";
    private const string ProviderAssemblyName = "XpoNoSQL.MongoDB.dll";
    private const string ProviderTypeName = "XpoMongoProvider.MongoDataStore";
    private IDisposable[] providerDisposables = Array.Empty<IDisposable>();
    private string connectionUri = "mongodb://localhost:27017";

    public IDataLayer DataLayer { get; private set; } = null!;

    public Task InitializeAsync()
    {
        //BuildSqLite();
        BuildXpoNoSql();
        return Task.CompletedTask;
    }

    private void BuildSqLite()
    {
        var connectionString = @"Integrated Security=SSPI;Pooling=false;Data Source=(localdb)\mssqllocaldb;Initial Catalog=EMS2";
        var provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
        DataLayer = new ThreadSafeDataLayer(provider);

    }
    private void BuildXpoNoSql()
    {
        MongoConnectionProvider.Register(); // just to ensure Xpo can find our provider
        string connectionString = MongoConnectionProvider.GetConnectionString(connectionUri, DefaultDatabaseName);
        var provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
        DataLayer = new ThreadSafeDataLayer(provider);
    }

    public UnitOfWork NewUow()
    {
        return new UnitOfWork(DataLayer);
    }

    public void CleanupAll()
    {
        TestCleanup.ClearAll(this);
    }
    public void Cleanup<T>()
    {
        using (var uow = NewUow())
        {
            try
            {
                List<T> objects = uow.Query<T>().ToList();
                uow.Delete(objects);
                uow.CommitChanges();
            }
            catch (CannotLoadObjectsException ex)
            {

            }
        }
    }

    public Task DisposeAsync()
    {
        if (DataLayer is IDisposable disposableLayer)
        {
            disposableLayer.Dispose();
        }

        foreach (IDisposable disposable in providerDisposables)
        {
            disposable.Dispose();
        }
        providerDisposables = Array.Empty<IDisposable>();

        return Task.CompletedTask;
    }
}
