using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace QuizMasterServer.Models
{
    public class Exam
    {
        [BsonId]

        [BsonRepresentation(BsonType.ObjectId)]

        public string Id { get; set; }


        [BsonRepresentation(BsonType.ObjectId)]

        public string CreatedById { get; set; }

        public string Title { get; set; }

        public string? Description { get; set; }

        public int DurationMinutes { get; set; }

    }
}