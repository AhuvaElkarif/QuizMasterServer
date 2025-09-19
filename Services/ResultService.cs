using QuizMasterServer.Data;
using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace QuizMasterServer.Services
{
    public class ResultService : IResultService
    {
        private readonly IMongoDbContext _db;

        public ResultService(IMongoDbContext db)
        {
            _db = db;
        }

        public async Task<List<UngradedResultDto>> GetUngradedResultsAsync(ObjectId teacherId)
        {
            var teacherIdString = teacherId.ToString();
            if (!ObjectId.TryParse(teacherIdString, out ObjectId teacherObjectId))
                throw new ArgumentException("Invalid teacher ID");

            var pipeline = new[]
            {
                // Join with ExamAttempts first to get attempt data
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "ExamAttempts" },
                    { "localField", "ExamAttemptId" },
                    { "foreignField", "_id" },
                    { "as", "attempt" }
                }),
                new BsonDocument("$unwind", "$attempt"),
                
                // Join with Exams to get exam data
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "Exams" },
                    { "localField", "attempt.ExamId" },
                    { "foreignField", "_id" },
                    { "as", "exam" }
                }),
                new BsonDocument("$unwind", "$exam"),
                
                // Filter by teacher (only exams created by this teacher)
                new BsonDocument("$match", new BsonDocument("exam.CreatedById", teacherObjectId)),
                
                // Filter for ungraded results (no feedback or null feedback)
                new BsonDocument("$match", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("Feedback", BsonNull.Value),
                    new BsonDocument("Feedback", new BsonDocument("$exists", false))
                })),
                
                // Join with Students using the StudentId from attempt
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "Users" },
                    { "localField", "attempt.StudentId" },
                    { "foreignField", "_id" },
                    { "as", "student" }
                }),
                new BsonDocument("$unwind", new BsonDocument
                {
                    { "path", "$student" },
                    { "preserveNullAndEmptyArrays", true }
                }),
                
                // Project the final result
                new BsonDocument("$project", new BsonDocument
                {
                    { "ResultId", new BsonDocument("$toString", "$_id") },
                    { "Score", "$Score" },
                    { "StartedAt", "$attempt.StartedAt" },
                    { "SubmittedAt", "$attempt.SubmittedAt" },
                    { "ExamTitle", "$exam.Title" },
                    { "StudentId", new BsonDocument("$toString", "$attempt.StudentId") },
                    { "StudentName", new BsonDocument("$ifNull", new BsonArray { "$student.Username", "Unknown Student" }) },
                    { "ExamId", new BsonDocument("$toString", "$exam._id") }
                }),
            };

            var resultsRaw = await _db.Results
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            var results = resultsRaw.Select(d => new UngradedResultDto
            {
                ResultId = d.GetValue("ResultId").AsString,
                Score = d.GetValue("Score").AsInt32,
                StartedAt = d.Contains("StartedAt") ? d["StartedAt"].ToNullableUniversalTime() : null,
                SubmittedAt = d.Contains("SubmittedAt") ? d["SubmittedAt"].ToNullableUniversalTime() : null,
                ExamTitle = d.GetValue("ExamTitle").AsString,
                StudentId = d.GetValue("StudentId").AsString,
                StudentName = d.Contains("StudentName") ? d.GetValue("StudentName").AsString : "Unknown Student",
                ExamId = d.GetValue("ExamId").AsString
            }).ToList();

            return results;
        }

        public async Task UpdateFeedbackAsync(string resultId, string feedback, ObjectId teacherId)
        {
            var resultObjectId = ObjectId.Parse(resultId);

            // Verify ownership of exam via ExamAttempt and Exam
            var result = await _db.Results.Find(r => r.Id == resultObjectId).FirstOrDefaultAsync();
            if (result == null)
                throw new KeyNotFoundException("Result not found");

            var attempt = await _db.ExamAttempts.Find(ea => ea.Id == result.ExamAttemptId).FirstOrDefaultAsync();
            if (attempt == null)
                throw new KeyNotFoundException("Exam attempt not found");

            var exam = await _db.Exams.Find(e => e.Id == attempt.ExamId.ToString() && e.CreatedById == teacherId.ToString()).FirstOrDefaultAsync();
            if (exam == null)
                throw new UnauthorizedAccessException("Access denied");

            var update = Builders<Result>.Update.Set(r => r.Feedback, feedback);
            var updateResult = await _db.Results.UpdateOneAsync(r => r.Id == resultObjectId, update);

            if (updateResult.ModifiedCount == 0)
                throw new InvalidOperationException("Feedback update failed");
        }

        public async Task<List<BsonDocument>> GetAnalyticsAsync(ObjectId teacherId)
        {
            var pipeline = new[]
            {
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "ExamAttempts" },
                    { "localField", "ExamAttemptId" },
                    { "foreignField", "_id" },
                    { "as", "attempt" }
                }),
                new BsonDocument("$unwind", "$attempt"),
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "Exams" },
                    { "localField", "attempt.ExamId" },
                    { "foreignField", "_id" },
                    { "as", "exam" }
                }),
                new BsonDocument("$unwind", "$exam"),
                new BsonDocument("$match", new BsonDocument("exam.CreatedById", teacherId)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$exam._id" },
                    { "examTitle", new BsonDocument("$first", "$exam.Title") },
                    { "attemptCount", new BsonDocument("$sum", 1) },
                    { "averageScore", new BsonDocument("$avg", "$Score") },
                    { "totalQuestions", new BsonDocument("$first", new BsonDocument("$size", "$exam.Questions")) }
                })
            };

            var analytics = await _db.Results.Aggregate<BsonDocument>(pipeline).ToListAsync();
            return analytics;
        }
    }
}