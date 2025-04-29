namespace QuizMaster.DTO
{
    public class QuestionCreateRequest
    {
        public string Text { get; set; }
        public List<string> Options { get; set; }
        public string CorrectAnswer { get; set; }
        public string Category { get; set; }
        public int DifficultyLevel { get; set; }
    }

    public class QuestionUpdateRequest
    {
        public string Text { get; set; }
        public List<string> Options { get; set; }
        public string CorrectAnswer { get; set; }
        public string Category { get; set; }
        public int DifficultyLevel { get; set; }
    }
}
