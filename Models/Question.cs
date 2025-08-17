using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace QuizMasterServer.Models
{
    public enum QuestionType
    {
        MultipleChoice,
        TrueFalse,
        OpenText
    }
    public class Question
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ExamId { get; set; } = null!;

        public QuestionType QuestionType { get; set; }

        public string Text { get; set; }

        public List<string>? Options { get; set; } // For MCQ and TrueFalse (options like ["True", "False"])

        public List<string>? CorrectAnswers { get; set; }
    }
}
