using Amazon.Runtime.Internal.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using QuizMasterServer.Data;
using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
            var teacherIdStr = GetCurrentUserId().ToString();
            if (!ObjectId.TryParse(teacherIdStr, out ObjectId teacherObjectId))
                return BadRequest("Invalid teacher ID");

            var filter = Builders<Result>.Filter.Or(
                Builders<Result>.Filter.Eq("Feedback", BsonNull.Value),
                Builders<Result>.Filter.Exists("Feedback", false)
            );

            var pipeline = new[]
            {
        // First, apply the filter for ungraded results
        //new BsonDocument("$match", filter.ToBsonDocument()),
        
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
        
        // Now join with Students using the StudentId from attempt
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
            { "preserveNullAndEmptyArrays", true }  // keep results even if student missing
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

            Console.WriteLine($"Documents returned: {resultsRaw.Count}");

            // Map raw BsonDocuments to DTOs
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

            return Ok(results);
        }

        //        [HttpGet("ungraded")]

        //        public async Task<IActionResult> GetUngradedResults()

        //        {

        //            var teacherIdStr = GetCurrentUserId().ToString();

        //            if (!ObjectId.TryParse(teacherIdStr, out ObjectId teacherObjectId))

        //                return BadRequest("Invalid teacher ID");


        //            var filter = Builders<Result>.Filter.Or(

        //                Builders<Result>.Filter.Eq("Feedback", BsonNull.Value),

        //                Builders<Result>.Filter.Exists("Feedback", false)

        //            );


        //            var pipeline = new[]
        // {
        //                 new BsonDocument("$lookup", new BsonDocument

        //        {

        //            { "from", "Students" },

        //            { "localField", "attempt.StudentId" },

        //            { "foreignField", "_id" },

        //            { "as", "student" }

        //        }),

        //        new BsonDocument("$unwind", new BsonDocument

        //        {

        //            { "path", "$student" },

        //            { "preserveNullAndEmptyArrays", true }  // keep results even if student missing

        //        }),
        //    new BsonDocument("$lookup", new BsonDocument
        //    {
        //        { "from", "ExamAttempts" },
        //        { "localField", "ExamAttemptId" },
        //        { "foreignField", "_id" },
        //        { "as", "attempt" }
        //    }),
        //    new BsonDocument("$unwind", "$attempt"),
        //    new BsonDocument("$lookup", new BsonDocument
        //    {
        //        { "from", "Exams" },
        //        { "localField", "attempt.ExamId" },
        //        { "foreignField", "_id" },
        //        { "as", "exam" }
        //    }),
        //    new BsonDocument("$unwind", "$exam"),
        //    new BsonDocument("$match", new BsonDocument("exam.CreatedById", teacherObjectId)),
        //    new BsonDocument("$project", new BsonDocument
        //    {
        //      { "ResultId", new BsonDocument("$toString", "$_id") },
        //      { "Score", "$Score" },
        //      { "StartedAt", "$attempt.StartedAt" },
        //      { "SubmittedAt", "$attempt.SubmittedAt" },
        //      { "ExamTitle", "$exam.Title" },
        //      { "StudentId", new BsonDocument("$toString", "$attempt.StudentId") },
        //                  { "StudentName", new BsonDocument("$ifNull", new BsonArray { "$student.Username", "Unknown Student" }) },
        //      { "ExamId", new BsonDocument("$toString", "$exam._id") }
        //    }),
        //};

        //            var resultsRaw = await _db.Results
        //                .Aggregate<BsonDocument>(pipeline)
        //                .ToListAsync();

        //            Console.WriteLine($"Documents returned: {resultsRaw.Count}");




        //            // Map raw BsonDocuments to DTOs

        //            var results = resultsRaw.Select(d => new UngradedResultDto

        //            {

        //                ResultId = d.GetValue("ResultId").AsString,

        //                Score = d.GetValue("Score").AsInt32,

        //                StartedAt = d.Contains("StartedAt") ? d["StartedAt"].ToNullableUniversalTime() : null,

        //                SubmittedAt = d.Contains("SubmittedAt") ? d["SubmittedAt"].ToNullableUniversalTime() : null,

        //                ExamTitle = d.GetValue("ExamTitle").AsString,

        //                StudentId = d.GetValue("StudentId").AsString,

        //                StudentName = d.Contains("StudentName") ? d.GetValue("StudentName").AsString : "Unknown Student",

        //                ExamId = d.GetValue("ExamId").AsString

        //            }).ToList();


        //            return Ok(results);

        //        }
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