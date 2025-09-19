using QuizMasterServer.Data;
using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuizMasterServer.Services
{
    public class ExamService : IExamService
    {
        private readonly IMongoDbContext _db;

        public ExamService(IMongoDbContext db)
        {
            _db = db;
        }

        public async Task<List<ExamDto>> GetMyExamsAsync(ObjectId teacherId)
        {
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

            return examsDto;
        }

        public async Task<ExamWithQuestionsDto> GetExamAsync(string examId, ObjectId userId, string userRole)
        {
            if (!ObjectId.TryParse(examId, out var examObjectId))
                throw new ArgumentException("Invalid exam id");

            var userIdString = userId.ToString();
            Exam exam;

            if (userRole == "Teacher")
            {
                exam = await _db.Exams.Find(e => e.Id == examId && e.CreatedById == userIdString).FirstOrDefaultAsync();
            }
            else if (userRole == "Student")
            {
                exam = await _db.Exams.Find(e => e.Id == examId).FirstOrDefaultAsync();
            }
            else
            {
                throw new UnauthorizedAccessException("Invalid user role");
            }

            if (exam == null)
                throw new KeyNotFoundException("Exam not found");

            var questions = await _db.Questions.Find(q => q.ExamId == examId).ToListAsync();

            var questionDtos = questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                QuestionType = q.QuestionType,
                Text = q.Text,
                Options = q.Options,
                CorrectAnswers = q.CorrectAnswers
            }).ToList();

            var dto = new ExamWithQuestionsDto
            {
                Id = exam.Id,
                Title = exam.Title,
                Description = exam.Description,
                DurationMinutes = exam.DurationMinutes,
                Questions = questionDtos
            };

            return dto;
        }

        public async Task<ExamDto> CreateExamAsync(ExamCreateDto examCreateDto, ObjectId teacherId)
        {
            var exam = new Exam
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Title = examCreateDto.Title,
                Description = examCreateDto.Description,
                DurationMinutes = examCreateDto.DurationMinutes,
                CreatedById = teacherId.ToString()
            };

            await _db.Exams.InsertOneAsync(exam);

            var dto = new ExamDto
            {
                Id = exam.Id,
                Title = exam.Title,
                Description = exam.Description,
                DurationMinutes = exam.DurationMinutes
            };

            return dto;
        }

        public async Task UpdateExamAsync(string examId, ExamUpdateDto updatedExamDto, ObjectId teacherId)
        {
            if (!ObjectId.TryParse(examId, out var examObjectId))
                throw new ArgumentException("Invalid exam id");

            var teacherIdString = teacherId.ToString();
            var existingExam = await _db.Exams.Find(e => e.Id == examId && e.CreatedById == teacherIdString).FirstOrDefaultAsync();

            if (existingExam == null)
                throw new KeyNotFoundException("Exam not found or access denied");

            var updatedExam = new Exam
            {
                Id = examId,
                Title = updatedExamDto.Title,
                Description = updatedExamDto.Description,
                DurationMinutes = updatedExamDto.DurationMinutes,
                CreatedById = teacherIdString
            };

            var examResult = await _db.Exams.ReplaceOneAsync(
                e => e.Id == examId && e.CreatedById == teacherIdString,
                updatedExam
            );

            if (!examResult.IsAcknowledged || examResult.MatchedCount == 0)
                throw new InvalidOperationException("Exam update failed");

            if (updatedExamDto.Questions != null)
            {
                foreach (var questionDto in updatedExamDto.Questions)
                {
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
                        ExamId = examId,
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
                        throw new InvalidOperationException($"Failed to update question with id {question.Id}");
                    }
                }
            }
        }

        public async Task DeleteExamAsync(string examId, ObjectId teacherId)
        {
            if (!ObjectId.TryParse(examId, out var examObjectId))
                throw new ArgumentException("Invalid exam id");

            var teacherIdString = teacherId.ToString();
            var result = await _db.Exams.DeleteOneAsync(e => e.Id == examId && e.CreatedById == teacherIdString);

            if (result.DeletedCount == 0)
                throw new KeyNotFoundException("Exam not found or access denied");
        }
    }
}