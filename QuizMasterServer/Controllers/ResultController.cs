using QuizMasterServer.Data;
using QuizMasterServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "TeacherOnly")]
    public class ResultController : ControllerBase
    {
        private readonly IMongoDbContext _db;

        public ResultController(IMongoDbContext db)
        {
            _db = db;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        [HttpGet("ungraded")]
        public async Task<IActionResult> GetUngradedResults()
        {
            var teacherId = GetCurrentUserId();

            var filter = Builders<Result>.Filter.Eq("Feedback", BsonNull.Value);

            // Join with exam attempts and exams, filter exams by teacherId
            var ungradedResults = await _db.Results.Aggregate()
                .Match(filter)
                .Lookup("ExamAttempts", "ExamAttemptId", "_id", "attempt")
                .Unwind("attempt")
                .Lookup("Exams", "attempt.ExamId", "_id", "exam")
                .Unwind("exam")
                .Match(Builders<BsonDocument>.Filter.Eq("exam.CreatedById", teacherId))
                .Project(new BsonDocument
                {
                    { "ResultId", "$_id" },
                    { "Score", "$Score" },
                    { "StartedAt", "$attempt.StartedAt" },
                    { "SubmittedAt", "$attempt.SubmittedAt" },
                    { "ExamTitle", "$exam.Title" },
                    { "StudentId", "$attempt.StudentId" }
                })
                .ToListAsync();

            // Populate Student usernames - this requires additional queries or embedding in response here simplified

            return Ok(ungradedResults);
        }

        public class FeedbackDto
        {
            public string Feedback { get; set; }
        }

        [HttpPut("{id:length(24)}/feedback")]
        public async Task<IActionResult> UpdateFeedback(string id, FeedbackDto dto)
        {
            var teacherId = GetCurrentUserId();
            var resultId = ObjectId.Parse(id);

            // Verify ownership of exam via ExamAttempt and Exam
            var result = await _db.Results.Find(r => r.Id == resultId).FirstOrDefaultAsync();
            if (result == null) return NotFound();

            var attempt = await _db.ExamAttempts.Find(ea => ea.Id == result.ExamAttemptId).FirstOrDefaultAsync();
            if (attempt == null) return NotFound();

            var exam = await _db.Exams.Find(e => e.Id == attempt.ExamId.ToString() && e.CreatedById == teacherId.ToString()).FirstOrDefaultAsync();
            if (exam == null) return Forbid();

            var update = Builders<Result>.Update.Set(r => r.Feedback, dto.Feedback);
            var resUpdate = await _db.Results.UpdateOneAsync(r => r.Id == resultId, update);
            if (resUpdate.ModifiedCount == 0) return StatusCode(500, "Feedback update failed");

            return NoContent();
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            var teacherId = GetCurrentUserId();

            // group results by exam and calculate average score and attempt count
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

            return Ok(analytics);
        }
    }
}