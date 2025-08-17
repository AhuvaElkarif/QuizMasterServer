using Microsoft.Extensions.Options;
using MongoDB.Driver;
using QuizMasterServer.Models;

namespace QuizMasterServer.Data
{
    public class MongoDbContext : IMongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IOptions<MongoDbSettings> options)
        {
            var client = new MongoClient(options.Value.ConnectionString);
            _database = client.GetDatabase(options.Value.DatabaseName);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");

        public IMongoCollection<Exam> Exams => _database.GetCollection<Exam>("Exams");

        public IMongoCollection<Question> Questions => _database.GetCollection<Question>("Questions");

        public IMongoCollection<ExamAttempt> ExamAttempts => _database.GetCollection<ExamAttempt>("ExamAttempts");

        public IMongoCollection<Answer> Answers => _database.GetCollection<Answer>("Answers");

        public IMongoCollection<Result> Results => _database.GetCollection<Result>("Results");
    }
}
