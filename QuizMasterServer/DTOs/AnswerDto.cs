namespace QuizMasterServer.DTOs
{
    public class AnswerDto
    {
        public string QuestionId { get; set; } = null!;

        public List<string> AnswerValues { get; set; } = new List<string>();
    }
}
