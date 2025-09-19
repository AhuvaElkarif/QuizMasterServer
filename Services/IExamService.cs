using QuizMasterServer.DTOs;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public interface IExamService
    {
        Task<List<ExamDto>> GetMyExamsAsync(ObjectId teacherId);
        Task<ExamWithQuestionsDto> GetExamAsync(string examId, ObjectId userId, string userRole);
        Task<ExamDto> CreateExamAsync(ExamCreateDto examCreateDto, ObjectId teacherId);
        Task UpdateExamAsync(string examId, ExamUpdateDto updatedExamDto, ObjectId teacherId);
        Task DeleteExamAsync(string examId, ObjectId teacherId);
    }
}