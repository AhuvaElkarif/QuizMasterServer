using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QuizMaster.Models
{

    public class Question
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Text { get; set; }

        public List<string> Options { get; set; }

        public string CorrectAnswer { get; set; }

        public string Category { get; set; }

        public int DifficultyLevel { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}