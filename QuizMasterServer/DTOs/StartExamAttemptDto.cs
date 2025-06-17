namespace QuizMasterServer.DTOs
{
    public class StartExamAttemptDto
    {
        public string ExamId { get; set; }
    }
    public class SubmitAnswerDto
    {
        public string QuestionId { get; set; }
        public string QuestionType { get; set; }
        public List<string> AnswerValues { get; set; }
    }
}