using QuizMasterServer.Models;
using MongoDB.Driver;
namespace QuizMasterServer.Data
{
    public interface IMongoDbContext
    {
        IMongoCollection<User> Users { get; }
        IMongoCollection<Exam> Exams { get; }
        IMongoCollection<Question> Questions { get; }
        IMongoCollection<ExamAttempt> ExamAttempts { get; }
        IMongoCollection<Answer> Answers { get; }
        IMongoCollection<Result> Results { get; }
    }
}
