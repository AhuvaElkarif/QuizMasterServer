using QuizMasterServer.Models;
using QuizMasterServer.DTOs;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public interface IStudentService
    {
        Task<List<Exam>> GetAvailableExamsAsync();
        Task<(bool Success, string Message, object Data)> StartExamAttemptAsync(string examId, ObjectId studentId);
        Task<(bool Success, string Message, object Data)> SubmitAnswersAsync(string attemptId, List<SubmitAnswerDto> submittedAnswersDto, ObjectId studentId);
        Task<List<object>> GetMyResultsAsync(ObjectId studentId);
        Task<List<AnswerDto>> GetAnswersForAttemptAsync(string examAttemptId);
    }
}