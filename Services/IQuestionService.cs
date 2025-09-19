using QuizMasterServer.Models;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public interface IQuestionService
    {
        Task<List<Question>> GetQuestionsAsync(string examId, ObjectId teacherId);
        Task<Question> GetQuestionAsync(string examId, string questionId, ObjectId teacherId);
        Task<Question> CreateQuestionAsync(string examId, Question question, ObjectId teacherId);
        Task UpdateQuestionAsync(string examId, string questionId, Question updatedQuestion, ObjectId teacherId);
        Task DeleteQuestionAsync(string examId, string questionId, ObjectId teacherId);
        Task<bool> ValidateExamOwnershipAsync(string examId, ObjectId teacherId);
    }
}