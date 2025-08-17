using QuizMasterServer.Data;
using QuizMasterServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/exam/{examId:length(24)}/[controller]")]
    [Authorize(Policy = "TeacherOnly")]
    public class QuestionController : ControllerBase
    {
        private readonly IMongoDbContext _db;

        public QuestionController(IMongoDbContext db)
        {
            _db = db;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetQuestions(string examId)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            // IDs stored as strings, so convert ObjectId to string for comparison
            var teacherIdString = teacherId.ToString();
            var examIdString = examId;

            var exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found or not your exam");

            var questions = await _db.Questions.Find(q => q.ExamId == examIdString).ToListAsync();
            return Ok(questions);
        }

        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<Question>> GetQuestion(string examId, string id)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            var teacherIdString = teacherId.ToString();
            var examIdString = examId;
            var questionIdString = id;

            var exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found or not your exam");

            var question = await _db.Questions.Find(q => q.Id == questionIdString && q.ExamId == examIdString).FirstOrDefaultAsync();
            if (question == null) return NotFound();
            return Ok(question);
        }

        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(string examId, [FromBody] Question question)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            var teacherIdString = teacherId.ToString();
            var examIdString = examId;

            var exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found or not your exam");

            question.Id = ObjectId.GenerateNewId().ToString();
            question.ExamId = examIdString;

            await _db.Questions.InsertOneAsync(question);
            return CreatedAtAction(nameof(GetQuestion), new { examId, id = question.Id }, question);
        }

        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> UpdateQuestion(string examId, string id, [FromBody] Question updatedQuestion)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            var teacherIdString = teacherId.ToString();
            var examIdString = examId;
            var questionIdString = id;

            var exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found or not your exam");


            var question = await _db.Questions.Find(q => q.Id == questionIdString && q.ExamId == examIdString).FirstOrDefaultAsync();
            if (question == null) return NotFound();

            updatedQuestion.Id = questionIdString;
            updatedQuestion.ExamId = examIdString;

            var result = await _db.Questions.ReplaceOneAsync(q => q.Id == questionIdString && q.ExamId == examIdString, updatedQuestion);
            if (result.ModifiedCount > 0) return NoContent();

            return StatusCode(500, "Update failed");
        }

        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> DeleteQuestion(string examId, string id)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            var teacherIdString = teacherId.ToString();
            var examIdString = examId;
            var questionIdString = id;

            var exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found or not your exam");

            var result = await _db.Questions.DeleteOneAsync(q => q.Id == questionIdString && q.ExamId == examIdString);
            if (result.DeletedCount == 0) return NotFound();

            return NoContent();
        }
    }
}