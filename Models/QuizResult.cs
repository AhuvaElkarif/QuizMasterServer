using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace QuizMaster.Models
{
    public class QuizResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string QuizId { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectAnswers { get; set; }

        public double Score { get; set; }

        public DateTime CompletedAt { get; set; } = DateTime.Now;
    }
}