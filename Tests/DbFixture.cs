using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Exceptions;

using DotNet.Testcontainers.Builders;

using System.ComponentModel;

using Testcontainers.MongoDb;

using XpoNoSQL.MongoDatabase.Core;

using Xunit;

namespace XpoNoSQL.MongoDatabase.Tests;

public sealed class DbFixture : IAsyncLifetime
{
    private MongoDbContainer _container;

    private const string DefaultDatabaseName = "xpo-mongo-tests";
    private IDisposable[] providerDisposables = Array.Empty<IDisposable>();
    private string connectionUri = "mongodb://localhost:27118";
    IDataStore provider;

    public IDataLayer DataLayer { get; private set; } = null!;

    public Task InitializeAsync()
    {
        //BuildSqlLocal();
        BuildForMongoDatabase();
        return Task.CompletedTask;
    }

    private void BuildSqlLocal()
    {
        var connectionString = $"Integrated Security=SSPI;Pooling=false;Data Source=(localdb)\\mssqllocaldb;Initial Catalog={DefaultDatabaseName}";
        provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
        DataLayer = new ThreadSafeDataLayer(provider);

    }
    private void BuildForMongoDatabase()
    {
        bool useContainer = true;
        if (useContainer) // use the container or a default local mongodb
        {
            _container = new MongoDbBuilder()
                    //.WithImage("mongo:7.0") // or whatever version you prefer
                    .WithPortBinding(27017, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(27017)))
                    .WithCleanUp(true)
                    .WithReuse(true)
                    .WithName($"{DefaultDatabaseName}")
                    .Build();

            _container.StartAsync().GetAwaiter().GetResult();

            connectionUri = _container.GetConnectionString();
        }
        // Ensure XPO knows how to resolve "mongodb://" connections
        MongoConnectionProvider.Register();
        string connectionString = MongoConnectionProvider.GetConnectionString(connectionUri, DefaultDatabaseName);
        provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
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
