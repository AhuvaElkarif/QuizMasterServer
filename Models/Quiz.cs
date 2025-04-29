using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
namespace QuizMaster.Models
{
    public class Quiz
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; }

        public List<QuizQuestion> Questions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public bool IsCompleted { get; set; } = false;
    }
}