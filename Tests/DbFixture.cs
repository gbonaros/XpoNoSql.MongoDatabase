using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Exceptions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using System.ComponentModel;

using Testcontainers.MongoDb;
using XpoNoSQL.MongoDatabase.Core;

using Xunit;

namespace XpoNoSql.Tests;

public sealed class DbFixture : IAsyncLifetime
{
    private DockerContainer _container;

    private const string DefaultDatabaseName = "xpo-tests";
    private IDisposable[] providerDisposables = Array.Empty<IDisposable>();
    private string connectionUri = "mongodb://localhost:27118";
    IDataStore provider;

    public IDataLayer DataLayer { get; private set; } = null!;

    public Task InitializeAsync()
    {
        //BuildSqlLocal();
        //BuildDynamoDB();
        BuildForMongoDatabase();
        return Task.CompletedTask;
    }

    private void BuildSqlLocal()
    {
        var connectionString = $"Integrated Security=SSPI;Pooling=false;Data Source=(localdb)\\mssqllocaldb;Initial Catalog={DefaultDatabaseName}";
        provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
        DataLayer = new ThreadSafeDataLayer(provider);

    }
    //private void BuildDynamoDB()
    //{
    //    var _container = new DynamoDbBuilder()
    //        .WithImage("amazon/dynamodb-local:latest")
    //        .WithName($"xponosql-dynamodb-{DefaultDatabaseName}")
    //        .WithCleanUp(false)
    //        .WithReuse(true)
    //        .Build();

    //    _container.StartAsync().GetAwaiter().GetResult();

    //    connectionUri = ((DynamoDbContainer)_container).GetConnectionString();

    //    DynamoConnectionProvider.Register();

    //    var connectionString = DynamoConnectionProvider.GetConnectionString(
    //        region: "us-east-1",           // any valid AWS region string works for local
    //        tablePrefix: "test_",          // optional namespace for test tables
    //        serviceUrl: connectionUri, 
    //        accessKey: "test",             // dummy creds accepted by DynamoDB Local/LocalStack
    //        secretKey: "test",             // dummy creds accepted by DynamoDB Local/LocalStack
    //        sessionToken: null,
    //        profile: null);

    //    provider = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.DatabaseAndSchema);
    //    DataLayer = new ThreadSafeDataLayer(provider);
    //}
    private void BuildForMongoDatabase()
    {
        bool useContainer = true;
        if (useContainer) // use the container or a default local mongodb
        {
            _container = new MongoDbBuilder()
                    .WithPortBinding(27017, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(27017)))
                    .WithCleanUp(false)
                    .WithReuse(true)
                    .WithName($"xponosql-mongodb-{DefaultDatabaseName}")
                    .Build();

            _container.StartAsync().GetAwaiter().GetResult();

            connectionUri = ((MongoDbContainer)_container).GetConnectionString();
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
