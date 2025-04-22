using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ResultController : ControllerBase
{
    private readonly IMongoCollection<Result> _resultsCollection;
    private readonly IMongoCollection<Exam> _examsCollection;
    private readonly IMongoCollection<Question> _questionsCollection;

    public ResultController(MongoDBService mongoDBService)
    {
        _resultsCollection = mongoDBService.GetCollection<Result>("Results");
        _examsCollection = mongoDBService.GetCollection<Exam>("Exams");
        _questionsCollection = mongoDBService.GetCollection<Question>("Questions");
    }

    [HttpGet]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetAllResults()
    {
        var results = await _resultsCollection.Find(_ => true).ToListAsync();
        return Ok(results);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserResults(string userId)
    {
        // Only allow users to see their own results, or teachers to see any results
        string currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != userId && userRole != "Teacher")
            return Forbid();

        var results = await _resultsCollection.Find(r => r.UserId == userId).ToListAsync();
        return Ok(results);
    }

    [HttpGet("exam/{examId}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetExamResults(string examId)
    {
        var results = await _resultsCollection.Find(r => r.ExamId == examId).ToListAsync();
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitExam(ExamSubmissionModel submission)
    {
        // Get the current user ID
        string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Get the exam to verify questions
        var exam = await _examsCollection.Find(e => e.Id == submission.ExamId).FirstOrDefaultAsync();
        if (exam == null)
            return BadRequest("Exam not found");

        // Check if all questions in submission belong to the exam
        foreach (var answer in submission.Answers)
        {
            if (!exam.QuestionIds.Contains(answer.QuestionId))
                return BadRequest($"Question {answer.QuestionId} is not part of this exam");
        }

        // Calculate score
        int score = 0;
        foreach (var answer in submission.Answers)
        {
            var question = await _questionsCollection.Find(q => q.Id == answer.QuestionId).FirstOrDefaultAsync();
            if (question != null && question.CorrectAnswerIndex == answer.SelectedOptionIndex)
            {
                score++;
            }
        }

        // Create and save the result
        var result = new Result
        {
            UserId = userId,
            ExamId = submission.ExamId,
            Score = score
        };

        await _resultsCollection.InsertOneAsync(result);

        return CreatedAtAction(nameof(GetUserResults), new { userId }, result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteResult(string id)
    {
        var result = await _resultsCollection.DeleteOneAsync(r => r.Id == id);

        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }
}

// Models for request validation
public class ExamSubmissionModel
{
    public string ExamId { get; set; }
    public List<QuestionAnswer> Answers { get; set; }
}

public class QuestionAnswer
{
    public string QuestionId { get; set; }
    public int SelectedOptionIndex { get; set; }
}