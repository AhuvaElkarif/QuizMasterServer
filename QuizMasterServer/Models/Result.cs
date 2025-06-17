using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace QuizMasterServer.Models
{
    public class Result
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public ObjectId ExamAttemptId { get; set; }
        public ObjectId ExamId { get; set; }


        public int Score { get; set; }

        public string? Feedback { get; set; }
    }
}