using QuizMasterServer.Data;
using QuizMasterServer.Models;
using QuizMasterServer.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public class StudentService : IStudentService
    {
        private readonly IMongoDbContext _db;

        public StudentService(IMongoDbContext db)
        {
            _db = db;
        }

        public async Task<List<Exam>> GetAvailableExamsAsync()
        {
            var exams = await _db.Exams.Find(_ => true).ToListAsync();
            return exams;
        }

        public async Task<(bool Success, string Message, object Data)> StartExamAttemptAsync(string examId, ObjectId studentId)
        {
            if (string.IsNullOrEmpty(examId) || !ObjectId.TryParse(examId, out var examObjectId))
                return (false, "Invalid exam id", null);

            var exam = await _db.Exams.Find(e => e.Id == examId).FirstOrDefaultAsync();
            if (exam == null)
                return (false, "Exam not found", null);

            var existingAttempt = await _db.ExamAttempts.Find(ea => ea.ExamId == examObjectId && ea.StudentId == studentId && ea.SubmittedAt == null).FirstOrDefaultAsync();
            if (existingAttempt != null)
                return (false, "You have an unfinished attempt for this exam", null);

            var attempt = new ExamAttempt
            {
                Id = ObjectId.GenerateNewId(),
                ExamId = examObjectId,
                StudentId = studentId,
                StartedAt = DateTime.UtcNow
            };

            await _db.ExamAttempts.InsertOneAsync(attempt);
            return (true, "Exam attempt started successfully", new { id = attempt.Id.ToString(), durationMinutes = exam.DurationMinutes });
        }

        public async Task<(bool Success, string Message, object Data)> SubmitAnswersAsync(string attemptId, List<SubmitAnswerDto> submittedAnswersDto, ObjectId studentId)
        {
            if (!ObjectId.TryParse(attemptId, out var attemptObjectId))
                return (false, "Invalid attempt ID", null);

            var attempt = await _db.ExamAttempts.Find(a => a.Id == attemptObjectId && a.StudentId == studentId).FirstOrDefaultAsync();
            if (attempt == null)
                return (false, "Exam attempt not found", null);

            if (attempt.SubmittedAt != null)
                return (false, "This attempt is already submitted", null);

            // Validate questions belong to exam
            var questionIds = (await _db.Questions.Find(q => q.ExamId == attempt.ExamId.ToString())
                                            .Project(q => q.Id)
                                            .ToListAsync()).ToHashSet();

            if (submittedAnswersDto.Any(a => !ObjectId.TryParse(a.QuestionId, out _) || !questionIds.Contains(a.QuestionId)))
                return (false, "Some answers refer to invalid questions for this exam", null);

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

            // Calculate score
            var score = await CalculateScoreAsync(attempt.ExamId.ToString(), submittedAnswers);
            var questions = await _db.Questions.Find(q => q.ExamId == attempt.ExamId.ToString()).ToListAsync();

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

            return (true, "Answers submitted successfully", new { score, totalQuestions = questions.Count });
        }

        public async Task<List<object>> GetMyResultsAsync(ObjectId studentId)
        {
            try
            {
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
                        ExamAttemptId = result.ExamAttemptId.ToString(),
                        ExamId = result.ExamId.ToString(),
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetMyResults: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new Exception($"Error retrieving results: {ex.Message}");
            }
        }

        public async Task<List<AnswerDto>> GetAnswersForAttemptAsync(string examAttemptId)
        {
            if (!ObjectId.TryParse(examAttemptId, out var attemptObjectId))
                throw new ArgumentException("Invalid examAttemptId format");

            var filter = Builders<Answer>.Filter.Eq(a => a.ExamAttemptId, attemptObjectId);
            var answers = await _db.Answers.Find(filter).ToListAsync();

            // Map to DTO for response
            var result = new List<AnswerDto>();
            foreach (var a in answers)
            {
                result.Add(new AnswerDto()
                {
                    QuestionId = a.QuestionId.ToString(),
                    AnswerValues = a.AnswerValues ?? new List<string>()
                });
            }

            return result;
        }

        private async Task<int> CalculateScoreAsync(string examId, List<Answer> submittedAnswers)
        {
            var questions = await _db.Questions.Find(q => q.ExamId == examId).ToListAsync();
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

            return score;
        }
    }
}