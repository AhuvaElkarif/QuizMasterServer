using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace QuizMaster.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Username { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public string Role { get; set; } // "Teacher" or "Student"

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}