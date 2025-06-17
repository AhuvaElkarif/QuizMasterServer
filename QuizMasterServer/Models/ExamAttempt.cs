using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace QuizMasterServer.Models
{
    public class ExamAttempt
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public ObjectId ExamId { get; set; }

        public ObjectId StudentId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }
    }
}