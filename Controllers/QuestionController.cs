using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class QuestionController : ControllerBase
{
    private readonly IMongoCollection<Question> _questionsCollection;

    public QuestionController(MongoDBService mongoDBService)
    {
        _questionsCollection = mongoDBService.GetCollection<Question>("Questions");
    }

    [HttpGet]
    public async Task<IActionResult> GetAllQuestions()
    {
        var questions = await _questionsCollection.Find(_ => true).ToListAsync();
        return Ok(questions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuestion(string id)
    {
        var question = await _questionsCollection.Find(q => q.Id == id).FirstOrDefaultAsync();

        if (question == null)
            return NotFound();

        return Ok(question);
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuestion(Question question)
    {
        await _questionsCollection.InsertOneAsync(question);
        return CreatedAtAction(nameof(GetQuestion), new { id = question.Id }, question);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateQuestion(string id, Question questionIn)
    {
        var result = await _questionsCollection.ReplaceOneAsync(q => q.Id == id, questionIn);

        if (result.ModifiedCount == 0)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteQuestion(string id)
    {
        var result = await _questionsCollection.DeleteOneAsync(q => q.Id == id);

        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }
}