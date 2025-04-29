using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace QuizMaster.Models
{
    public class QuizQuestion
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string QuestionId { get; set; }

        public string Text { get; set; }

        public List<string> Options { get; set; }

        public string StudentAnswer { get; set; }

        public string CorrectAnswer { get; set; }
    }
}