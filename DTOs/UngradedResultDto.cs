namespace QuizMasterServer.DTOs
{
    public class UngradedResultDto

    {

        public string ResultId { get; set; } = null!;
        public string ExamId { get; set; } = null!;

        public string StudentName { get; set; } = null!;
        public int Score { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public string ExamTitle { get; set; } = null!;

        public string StudentId { get; set; } = null!;

    }
}
