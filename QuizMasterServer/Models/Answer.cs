using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace QuizMasterServer.Models
{
    public class Answer
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public ObjectId ExamAttemptId { get; set; }

        public ObjectId QuestionId { get; set; }

        public QuestionType QuestionType { get; set; }

        public List<string>? AnswerValues { get; set; }
    }
}
