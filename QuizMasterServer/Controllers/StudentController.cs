using QuizMasterServer.Data;
using QuizMasterServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using QuizMasterServer.DTOs;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StudentOnly")]
    public class StudentController : ControllerBase
    {
        private readonly IMongoDbContext _db;

        public StudentController(IMongoDbContext db)
        {
            _db = db;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        [HttpGet("exams")]
        public async Task<IActionResult> GetAvailableExams()
        {
            var exams = await _db.Exams.Find(_ => true).ToListAsync();
            return Ok(exams);
        }

        [HttpPost("examattempt/start")]
        public async Task<IActionResult> StartExamAttempt([FromBody] StartExamAttemptDto dto)
        {
            if (string.IsNullOrEmpty(dto.ExamId) || !ObjectId.TryParse(dto.ExamId, out var examId))
                return BadRequest("Invalid exam id");

            var studentId = GetCurrentUserId();
            var exam = await _db.Exams.Find(e => e.Id == dto.ExamId).FirstOrDefaultAsync();
            if (exam == null) return NotFound("Exam not found");

            var existingAttempt = await _db.ExamAttempts.Find(ea => ea.ExamId == examId && ea.StudentId == studentId && ea.SubmittedAt == null).FirstOrDefaultAsync();
            if (existingAttempt != null) return Conflict("You have an unfinished attempt for this exam");

            var attempt = new ExamAttempt
            {
                Id = ObjectId.GenerateNewId(),
                ExamId = examId,
                StudentId = studentId,
                StartedAt = DateTime.UtcNow
            };

            await _db.ExamAttempts.InsertOneAsync(attempt);
            return Ok(new { id = attempt.Id.ToString(), durationMinutes = exam.DurationMinutes });
        }

        [HttpPost("examattempt/{attemptId:length(24)}/submit")]
        public async Task<IActionResult> SubmitAnswers(string attemptId, [FromBody] List<SubmitAnswerDto> submittedAnswersDto)
        {
            var studentId = GetCurrentUserId();
            if (!ObjectId.TryParse(attemptId, out var attemptObjectId))
                return BadRequest("Invalid attempt ID");

            var attempt = await _db.ExamAttempts.Find(a => a.Id == attemptObjectId && a.StudentId == studentId).FirstOrDefaultAsync();
            if (attempt == null) return NotFound("Exam attempt not found");
            if (attempt.SubmittedAt != null) return BadRequest("This attempt is already submitted");

            // Validate questions belong to exam
            var questionIds = (await _db.Questions.Find(q => q.ExamId == attempt.ExamId.ToString())
                                            .Project(q => q.Id)
                                            .ToListAsync()).ToHashSet();

            if (submittedAnswersDto.Any(a => !ObjectId.TryParse(a.QuestionId, out _) || !questionIds.Contains(a.QuestionId)))
                return BadRequest("Some answers refer to invalid questions for this exam");

            // Convert DTOs to Answer models and insert
            var submittedAnswers = new List<Answer>();
            foreach (var answerDto in submittedAnswersDto)
            {
                var questionObjectId = ObjectId.Parse(answerDto.QuestionId);

                var answer = new Answer
                {
                    Id = ObjectId.GenerateNewId(),
                    ExamAttemptId = attemptObjectId,
                    QuestionId = questionObjectId,
                    AnswerValues = answerDto.AnswerValues ?? new List<string>()
                };

                submittedAnswers.Add(answer);
            }

            // Bulk insert answers for efficiency
            if (submittedAnswers.Count > 0)
                await _db.Answers.InsertManyAsync(submittedAnswers);

            // Fetch questions with CorrectAnswers and QuestionType
            var questions = await _db.Questions.Find(q => q.ExamId == attempt.ExamId.ToString()).ToListAsync();

            int score = 0;
            foreach (var question in questions)
            {
                var answer = submittedAnswers.FirstOrDefault(a => a.QuestionId.ToString() == question.Id);
                if (answer == null) continue;

                bool correct = false;

                switch (question.QuestionType)
                {
                    case QuestionType.MultipleChoice:
                    case QuestionType.TrueFalse:
                        if (question.CorrectAnswers != null && answer.AnswerValues != null)
                        {
                            // Normalize answers: trim & lowercase to avoid casing/space issues
                            var expected = question.CorrectAnswers.Select(x => x?.Trim().ToLowerInvariant() ?? string.Empty).OrderBy(x => x);
                            var actual = answer.AnswerValues.Select(x => x?.Trim().ToLowerInvariant() ?? string.Empty).OrderBy(x => x);
                            correct = expected.SequenceEqual(actual);
                        }
                        break;

                    case QuestionType.OpenText:
                        // No auto grading; could be improved to manual grading later
                        correct = false;
                        break;
                }

                if (correct) score++;
            }

            // Save Result with ExamId for reference
            var result = new Result()
            {
                Id = ObjectId.GenerateNewId(),
                ExamAttemptId = attempt.Id,
                ExamId = attempt.ExamId, 
                Score = score,
                Feedback = null,
            };

            await _db.Results.InsertOneAsync(result);

            // Mark attempt as submitted
            var updateAttempt = Builders<ExamAttempt>.Update.Set(ea => ea.SubmittedAt, DateTime.UtcNow);
            await _db.ExamAttempts.UpdateOneAsync(ea => ea.Id == attempt.Id, updateAttempt);

            return Ok(new { score, totalQuestions = questions.Count });
        }

        [HttpGet("results")]
        public async Task<IActionResult> GetMyResults()
        {
            try
            {
                var studentId = GetCurrentUserId();

                // Option 1: Using multiple separate queries (more reliable)
                var examAttempts = await _db.ExamAttempts
                    .Find(ea => ea.StudentId == studentId && ea.SubmittedAt != null)
                    .ToListAsync();

                var results = new List<object>();

                foreach (var attempt in examAttempts)
                {
                    // Get the result for this attempt
                    var result = await _db.Results
                        .Find(r => r.ExamAttemptId == attempt.Id)
                        .FirstOrDefaultAsync();

                    if (result == null) continue;

                    // Get the exam details
                    var exam = await _db.Exams
                        .Find(e => e.Id == attempt.ExamId.ToString())
                        .FirstOrDefaultAsync();

                    if (exam == null) continue;

                    results.Add(new
                    {
                        Id = result.Id.ToString(),
                        Score = result.Score,
                        Feedback = result.Feedback,
                        ExamTitle = exam.Title,
                        ExamId = result.ExamId,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetMyResults: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}