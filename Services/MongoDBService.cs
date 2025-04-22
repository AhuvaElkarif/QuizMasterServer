using Microsoft.Extensions.Options;
using MongoDB.Driver;

public class MongoDBService
{
    private readonly IMongoDatabase _database;

    public MongoDBService(IOptions<MongoDBSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }
    public IMongoCollection<T> GetCollection<T>(string name) => _database.GetCollection<T>(name);
}
