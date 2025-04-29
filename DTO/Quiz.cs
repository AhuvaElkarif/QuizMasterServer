namespace QuizMaster.DTO
{
    public class RandomQuizRequest
    {
        public int Count { get; set; } = 10;
    }

    public class QuizSubmitRequest
    {
        public Dictionary<string, string> Answers { get; set; }
    }
}