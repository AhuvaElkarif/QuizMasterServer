using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Security.Cryptography;
using System.Text;

namespace QuizMasterServer.Models
{
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(Teacher), typeof(Student))]
    public abstract class User
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("Username")]
        public string Username { get; set; }
        [BsonElement("Email")]
        public string Email { get; set; }

        [BsonElement("Role")]
        public string Role { get; set; } // "Teacher" or "Student"

        [BsonElement("PasswordHash")]
        public byte[] PasswordHash { get; set; }

        [BsonElement("PasswordSalt")]
        public byte[] PasswordSalt { get; set; }

        public void SetPassword(string password)
        {
            using var hmac = new HMACSHA512();
            PasswordSalt = hmac.Key;
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        public bool VerifyPassword(string password)
        {
            using var hmac = new HMACSHA512(PasswordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return computedHash.SequenceEqual(PasswordHash);
        }
    }
    public class Teacher : User
    {
        public Teacher()
        {
            Role = "Teacher";
        }
    }

    public class Student : User
    {
        public Student()
        {
            Role = "Student";
        }
    }
}