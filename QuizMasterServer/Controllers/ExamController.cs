using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using QuizMasterServer.Data;
using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuizMasterServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "TeacherOrStudent")]
    public class ExamController : ControllerBase
    {
        private readonly IMongoDbContext _db;

        public ExamController(IMongoDbContext db)
        {
            _db = db;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ObjectId.TryParse(userIdStr, out var objectId))
            {
                return ObjectId.Empty;
            }
            return objectId;
        }

        [HttpGet]
        public async Task<ActionResult<List<ExamDto>>> GetMyExams()
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            // Convert teacherId to string for comparison with CreatedById (string)
            var teacherIdString = teacherId.ToString();

            var exams = await _db.Exams.Find(e => e.CreatedById == teacherIdString).ToListAsync();

            var examsDto = new List<ExamDto>();
            foreach (var exam in exams)
            {
                examsDto.Add(new ExamDto
                {
                    Id = exam.Id,
                    Title = exam.Title,
                    Description = exam.Description,
                    DurationMinutes = exam.DurationMinutes
                });
            }

            return Ok(examsDto);
        }

        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<ExamWithQuestionsDto>> GetExam(string id)
        {
            var userId = GetCurrentUserId();
            if (userId == ObjectId.Empty)
                return Unauthorized();

            if (!ObjectId.TryParse(id, out var examId))
                return BadRequest("Invalid exam id");

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdString = userId.ToString();
            var examIdString = id;

            Exam exam;

            if (userRole == "Teacher")
            {
                // Teachers can only access their own exams
                exam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == userIdString).FirstOrDefaultAsync();
            }
            else if (userRole == "Student")
            {
                // Students can access any exam (you might want to add more restrictions here)
                exam = await _db.Exams.Find(e => e.Id == examIdString).FirstOrDefaultAsync();
            }
            else
            {
                return Forbid("Invalid user role");
            }

            if (exam == null)
                return NotFound();

            // Fetch questions from Questions collection where ExamId equals examId string
            var questions = await _db.Questions.Find(q => q.ExamId == examIdString).ToListAsync();

            // For students, you might want to hide correct answers
            var questionDtos = questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                QuestionType = q.QuestionType,
                Text = q.Text,
                Options = q.Options,
                CorrectAnswers = userRole == "Teacher" ? q.CorrectAnswers : new List<string>() // Hide answers for students
            }).ToList();

            // Create a DTO that includes exam info + questions
            var dto = new ExamWithQuestionsDto
            {
                Id = exam.Id,
                Title = exam.Title,
                Description = exam.Description,
                DurationMinutes = exam.DurationMinutes,
                Questions = questionDtos
            };

            return Ok(dto);
        }
        [HttpPost]
        public async Task<ActionResult<ExamDto>> CreateExam([FromBody] ExamCreateDto examCreateDto)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            var exam = new Exam
            {
                Id = ObjectId.GenerateNewId().ToString(),  // Store Id as string
                Title = examCreateDto.Title,
                Description = examCreateDto.Description,
                DurationMinutes = examCreateDto.DurationMinutes,
                CreatedById = teacherId.ToString()  // Store CreatedById as string
            };

            await _db.Exams.InsertOneAsync(exam);

            var dto = new ExamDto
            {
                Id = exam.Id,
                Title = exam.Title,
                Description = exam.Description,
                DurationMinutes = exam.DurationMinutes
            };

            return CreatedAtAction(nameof(GetExam), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> UpdateExam(string id, [FromBody] ExamUpdateDto updatedExamDto)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            if (!ObjectId.TryParse(id, out var examObjectId))
                return BadRequest("Invalid exam id");

            var teacherIdString = teacherId.ToString();
            var examIdString = id;

            var existingExam = await _db.Exams.Find(e => e.Id == examIdString && e.CreatedById == teacherIdString).FirstOrDefaultAsync();
            if (existingExam == null)
                return NotFound();

            // Update exam
            var updatedExam = new Exam
            {
                Id = examIdString,
                Title = updatedExamDto.Title,
                Description = updatedExamDto.Description,
                DurationMinutes = updatedExamDto.DurationMinutes,
                CreatedById = teacherIdString
            };

            var examResult = await _db.Exams.ReplaceOneAsync(
                e => e.Id == examIdString && e.CreatedById == teacherIdString,
                updatedExam
            );

            if (!examResult.IsAcknowledged || examResult.MatchedCount == 0)
                return StatusCode(500, "Exam update failed");

            // Upsert questions if any
            if (updatedExamDto.Questions != null)
            {
                foreach (var questionDto in updatedExamDto.Questions)
                {
                    // Validate or generate question ID as a valid ObjectId string
                    string questionId;
                    if (string.IsNullOrWhiteSpace(questionDto.Id) || !ObjectId.TryParse(questionDto.Id, out _))
                    {
                        questionId = ObjectId.GenerateNewId().ToString();
                    }
                    else
                    {
                        questionId = questionDto.Id.Trim();
                    }

                    var question = new Question
                    {
                        Id = questionId,
                        ExamId = examIdString,
                        QuestionType = questionDto.QuestionType,
                        Text = questionDto.Text,
                        Options = questionDto.Options,
                        CorrectAnswers = questionDto.CorrectAnswers
                    };

                    var filter = Builders<Question>.Filter.And(
                        Builders<Question>.Filter.Eq(q => q.Id, question.Id),
                        Builders<Question>.Filter.Eq(q => q.ExamId, question.ExamId)
                    );

                    var updateOptions = new ReplaceOptions { IsUpsert = true };

                    var questionResult = await _db.Questions.ReplaceOneAsync(filter, question, updateOptions);

                    if (!questionResult.IsAcknowledged)
                    {
                        return StatusCode(500, $"Failed to update question with id {question.Id}");
                    }
                }
            }

            return NoContent();
        }

        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> DeleteExam(string id)
        {
            var teacherId = GetCurrentUserId();
            if (teacherId == ObjectId.Empty)
                return Unauthorized();

            if (!ObjectId.TryParse(id, out var examId))
                return BadRequest("Invalid exam id");

            var teacherIdString = teacherId.ToString();
            var examIdString = id;

            var result = await _db.Exams.DeleteOneAsync(e => e.Id == examIdString && e.CreatedById == teacherIdString);
            if (result.DeletedCount == 0)
                return NotFound();

            return NoContent();
        }
    }
}