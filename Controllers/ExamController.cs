using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;

[Route("api/[controller]")]
[ApiController]
public class ExamController : ControllerBase
{
    private readonly IMongoCollection<Exam> _examsCollection;
    private readonly IMongoCollection<Question> _questionsCollection;

    public ExamController(MongoDBService mongoDBService)
    {
        _examsCollection = mongoDBService.GetCollection<Exam>("Exams");
        _questionsCollection = mongoDBService.GetCollection<Question>("Questions");
    }

    [HttpGet]
    public async Task<IActionResult> GetAllExams()
    {
        var exams = await _examsCollection.Find(_ => true).ToListAsync();
        return Ok(exams);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExam(string id)
    {
        var exam = await _examsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();

        if (exam == null)
            return NotFound();

        return Ok(exam);
    }

    [HttpGet("{id}/questions")]
    public async Task<IActionResult> GetExamWithQuestions(string id)
    {
        var exam = await _examsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();

        if (exam == null)
            return NotFound();

        // Get all questions for this exam
        var questions = await _questionsCollection
            .Find(q => exam.QuestionIds.Contains(q.Id))
            .ToListAsync();

        // Create a response with exam and questions
        var examWithQuestions = new ExamWithQuestionsModel
        {
            ExamId = exam.Id,
            Name = exam.Name,
            TimeLimitMinutes = exam.TimeLimitMinutes,
            Questions = questions
        };

        return Ok(examWithQuestions);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CreateExam(ExamCreateModel model)
    {
        // Validate that all question IDs exist
        if (model.QuestionIds != null && model.QuestionIds.Count > 0)
        {
            foreach (var questionId in model.QuestionIds)
            {
                var question = await _questionsCollection.Find(q => q.Id == questionId).FirstOrDefaultAsync();
                if (question == null)
                    return BadRequest($"Question with ID {questionId} not found");
            }
        }

        var exam = new Exam
        {
            Name = model.Name,
            QuestionIds = model.QuestionIds ?? new List<string>(),
            TimeLimitMinutes = model.TimeLimitMinutes
        };

        await _examsCollection.InsertOneAsync(exam);
        return CreatedAtAction(nameof(GetExam), new { id = exam.Id }, exam);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> UpdateExam(string id, ExamUpdateModel model)
    {
        var exam = await _examsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();

        if (exam == null)
            return NotFound();

        // Validate that all question IDs exist if provided
        if (model.QuestionIds != null && model.QuestionIds.Count > 0)
        {
            foreach (var questionId in model.QuestionIds)
            {
                var question = await _questionsCollection.Find(q => q.Id == questionId).FirstOrDefaultAsync();
                if (question == null)
                    return BadRequest($"Question with ID {questionId} not found");
            }
            exam.QuestionIds = model.QuestionIds;
        }

        if (!string.IsNullOrEmpty(model.Name))
            exam.Name = model.Name;

        if (model.TimeLimitMinutes > 0)
            exam.TimeLimitMinutes = model.TimeLimitMinutes;

        await _examsCollection.ReplaceOneAsync(e => e.Id == id, exam);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteExam(string id)
    {
        var result = await _examsCollection.DeleteOneAsync(e => e.Id == id);

        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id}/questions")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> AddQuestionsToExam(string id, List<string> questionIds)
    {
        var exam = await _examsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();

        if (exam == null)
            return NotFound();

        // Validate that all question IDs exist
        foreach (var questionId in questionIds)
        {
            var question = await _questionsCollection.Find(q => q.Id == questionId).FirstOrDefaultAsync();
            if (question == null)
                return BadRequest($"Question with ID {questionId} not found");

            // Add only if not already in list
            if (!exam.QuestionIds.Contains(questionId))
                exam.QuestionIds.Add(questionId);
        }

        await _examsCollection.ReplaceOneAsync(e => e.Id == id, exam);

        return NoContent();
    }

    [HttpDelete("{examId}/questions/{questionId}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> RemoveQuestionFromExam(string examId, string questionId)
    {
        var exam = await _examsCollection.Find(e => e.Id == examId).FirstOrDefaultAsync();

        if (exam == null)
            return NotFound();

        if (!exam.QuestionIds.Contains(questionId))
            return BadRequest("This question is not part of the exam");

        exam.QuestionIds.Remove(questionId);

        await _examsCollection.ReplaceOneAsync(e => e.Id == examId, exam);

        return NoContent();
    }
}

// Models for request validation
public class ExamCreateModel
{
    public string Name { get; set; }
    public List<string> QuestionIds { get; set; }
    public int TimeLimitMinutes { get; set; }
}

public class ExamUpdateModel
{
    public string Name { get; set; }
    public List<string> QuestionIds { get; set; }
    public int TimeLimitMinutes { get; set; }
}

public class ExamWithQuestionsModel
{
    public string ExamId { get; set; }
    public string Name { get; set; }
    public int TimeLimitMinutes { get; set; }
    public List<Question> Questions { get; set; }
}