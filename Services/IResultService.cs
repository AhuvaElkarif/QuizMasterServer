using QuizMasterServer.DTOs;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public interface IResultService
    {
        Task<List<UngradedResultDto>> GetUngradedResultsAsync(ObjectId teacherId);
        Task UpdateFeedbackAsync(string resultId, string feedback, ObjectId teacherId);
        Task<List<BsonDocument>> GetAnalyticsAsync(ObjectId teacherId);
    }
}