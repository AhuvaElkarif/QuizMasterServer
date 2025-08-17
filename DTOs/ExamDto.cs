using QuizMasterServer.Models;

namespace QuizMasterServer.DTOs
{
    public class ExamDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class ExamCreateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class ExamUpdateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public List<QuestionDto>? Questions { get; set; }
    }

    public class QuestionDto
    {
        public string? Id { get; set; }  // Optional, null for new questions
        public QuestionType QuestionType { get; set; }
        public string Text { get; set; } = null!;
        public List<string>? Options { get; set; }
        public List<string>? CorrectAnswers { get; set; }
    }
    public class ExamWithQuestionsDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public List<QuestionDto> Questions { get; set; } = new();
    }

}