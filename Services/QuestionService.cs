using QuizMasterServer.Data;
using QuizMasterServer.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IMongoDbContext _db;

        public QuestionService(IMongoDbContext db)
        {
            _db = db;
        }

        public async Task<bool> ValidateExamOwnershipAsync(string examId, ObjectId teacherId)
        {
            var teacherIdString = teacherId.ToString();
            var exam = await _db.Exams.Find(e => e.Id == examId && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            return exam != null;
        }

        public async Task<List<Question>> GetQuestionsAsync(string examId, ObjectId teacherId)
        {
            if (!await ValidateExamOwnershipAsync(examId, teacherId))
                throw new UnauthorizedAccessException("Exam not found or access denied");

            var questions = await _db.Questions.Find(q => q.ExamId == examId).ToListAsync();
            return questions;
        }

        public async Task<Question> GetQuestionAsync(string examId, string questionId, ObjectId teacherId)
        {
            if (!await ValidateExamOwnershipAsync(examId, teacherId))
                throw new UnauthorizedAccessException("Exam not found or access denied");

            var question = await _db.Questions.Find(q => q.Id == questionId && q.ExamId == examId).FirstOrDefaultAsync();

            if (question == null)
                throw new KeyNotFoundException("Question not found");

            return question;
        }

        public async Task<Question> CreateQuestionAsync(string examId, Question question, ObjectId teacherId)
        {
            if (!await ValidateExamOwnershipAsync(examId, teacherId))
                throw new UnauthorizedAccessException("Exam not found or access denied");

            question.Id = ObjectId.GenerateNewId().ToString();
            question.ExamId = examId;

            await _db.Questions.InsertOneAsync(question);
            return question;
        }

        public async Task UpdateQuestionAsync(string examId, string questionId, Question updatedQuestion, ObjectId teacherId)
        {
            if (!await ValidateExamOwnershipAsync(examId, teacherId))
                throw new UnauthorizedAccessException("Exam not found or access denied");

            var question = await _db.Questions.Find(q => q.Id == questionId && q.ExamId == examId).FirstOrDefaultAsync();
            if (question == null)
                throw new KeyNotFoundException("Question not found");

            updatedQuestion.Id = questionId;
            updatedQuestion.ExamId = examId;

            var result = await _db.Questions.ReplaceOneAsync(q => q.Id == questionId && q.ExamId == examId, updatedQuestion);

            if (result.ModifiedCount == 0)
                throw new InvalidOperationException("Update failed");
        }

        public async Task DeleteQuestionAsync(string examId, string questionId, ObjectId teacherId)
        {
            if (!await ValidateExamOwnershipAsync(examId, teacherId))
                throw new UnauthorizedAccessException("Exam not found or access denied");

            var result = await _db.Questions.DeleteOneAsync(q => q.Id == questionId && q.ExamId == examId);

            if (result.DeletedCount == 0)
                throw new KeyNotFoundException("Question not found");
        }
    }
}