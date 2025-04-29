using MongoDB.Driver;
using QuizMaster.DTO;
using QuizMaster.Models;

namespace QuizMaster.Services
{
    public class QuestionService
    {
        private readonly IMongoCollection<Question> _questions;

        public QuestionService(IMongoDatabase database)
        {
            _questions = database.GetCollection<Question>("Questions");
        }

        public async Task<List<Question>> GetAllAsync()
        {
            return await _questions.Find(question => true).ToListAsync();
        }

        public async Task<Question> GetByIdAsync(string id)
        {
            return await _questions.Find(question => question.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Question> CreateAsync(QuestionCreateRequest questionRequest, string teacherId)
        {
            var question = new Question
            {
                Text = questionRequest.Text,
                Options = questionRequest.Options,
                CorrectAnswer = questionRequest.CorrectAnswer,
                Category = questionRequest.Category,
                DifficultyLevel = questionRequest.DifficultyLevel,
                CreatedBy = teacherId,
                CreatedAt = DateTime.Now
            };

            await _questions.InsertOneAsync(question);
            return question;
        }

        public async Task<Question> UpdateAsync(string id, QuestionUpdateRequest questionRequest)
        {
            var question = await GetByIdAsync(id);
            if (question == null)
            {
                throw new Exception("Question not found");
            }

            var update = Builders<Question>.Update
                .Set(q => q.Text, questionRequest.Text)
                .Set(q => q.Options, questionRequest.Options)
                .Set(q => q.CorrectAnswer, questionRequest.CorrectAnswer)
                .Set(q => q.Category, questionRequest.Category)
                .Set(q => q.DifficultyLevel, questionRequest.DifficultyLevel);

            await _questions.UpdateOneAsync(q => q.Id == id, update);
            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(string id)
        {
            await _questions.DeleteOneAsync(question => question.Id == id);
        }

        public async Task<List<Question>> GetRandomQuestionsAsync(int count)
        {
            // Aggregate to get random questions
            return await _questions.Aggregate()
                .Sample(count)
                .ToListAsync();
        }
    }
}