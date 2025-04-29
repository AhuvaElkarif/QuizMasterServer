using MongoDB.Driver;
using QuizMaster.Models;

namespace QuizMaster.Services
{
    public class QuizService
    {
        private readonly IMongoCollection<Quiz> _quizzes;
        private readonly IMongoCollection<QuizResult> _quizResults;
        private readonly QuestionService _questionService;

        public QuizService(IMongoDatabase database, QuestionService questionService)
        {
            _quizzes = database.GetCollection<Quiz>("Quizzes");
            _quizResults = database.GetCollection<QuizResult>("QuizResults");
            _questionService = questionService;
        }

        public async Task<Quiz> CreateRandomQuizAsync(string studentId, int questionCount)
        {
            // Get random questions
            var questions = await _questionService.GetRandomQuestionsAsync(questionCount);

            // Convert to QuizQuestion objects
            var quizQuestions = questions.Select(q => new QuizQuestion
            {
                QuestionId = q.Id,
                Text = q.Text,
                Options = q.Options,
                CorrectAnswer = q.CorrectAnswer
            }).ToList();

            // Create quiz
            var quiz = new Quiz
            {
                StudentId = studentId,
                Questions = quizQuestions,
                CreatedAt = DateTime.Now
            };

            await _quizzes.InsertOneAsync(quiz);
            return quiz;
        }

        public async Task<Quiz> GetQuizAsync(string id)
        {
            return await _quizzes.Find(quiz => quiz.Id == id).FirstOrDefaultAsync();
        }

        public async Task<QuizResult> SubmitQuizAsync(string quizId, string studentId, Dictionary<string, string> answers)
        {
            var quiz = await GetQuizAsync(quizId);
            if (quiz == null)
            {
                throw new Exception("Quiz not found");
            }

            if (quiz.StudentId != studentId)
            {
                throw new Exception("This quiz does not belong to the current student");
            }

            if (quiz.IsCompleted)
            {
                throw new Exception("Quiz has already been completed");
            }

            // Update answers in the quiz
            foreach (var question in quiz.Questions)
            {
                if (answers.TryGetValue(question.QuestionId, out string answer))
                {
                    question.StudentAnswer = answer;
                }
            }

            // Calculate score
            int correctAnswers = quiz.Questions.Count(q => q.StudentAnswer == q.CorrectAnswer);
            double score = (double)correctAnswers / quiz.Questions.Count * 100;

            // Update quiz as completed
            var update = Builders<Quiz>.Update
                .Set(q => q.Questions, quiz.Questions)
                .Set(q => q.IsCompleted, true)
                .Set(q => q.CompletedAt, DateTime.Now);

            await _quizzes.UpdateOneAsync(q => q.Id == quizId, update);

            // Create quiz result
            var quizResult = new QuizResult
            {
                QuizId = quizId,
                StudentId = studentId,
                TotalQuestions = quiz.Questions.Count,
                CorrectAnswers = correctAnswers,
                Score = score,
                CompletedAt = DateTime.Now
            };

            await _quizResults.InsertOneAsync(quizResult);
            return quizResult;
        }

        public async Task<QuizResult> GetQuizResultAsync(string quizId)
        {
            return await _quizResults.Find(result => result.QuizId == quizId).FirstOrDefaultAsync();
        }

        public async Task<List<QuizResult>> GetStudentResultsAsync(string studentId)
        {
            return await _quizResults.Find(result => result.StudentId == studentId).ToListAsync();
        }
    }
}