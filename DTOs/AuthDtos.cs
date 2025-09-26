namespace QuizMasterServer.DTOs
{
    public class AuthDtos
    {
        public class RegisterRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string Role { get; set; } // "Teacher" or "Student"
        }

        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class AuthResponse
        {
            public string UserId { get; set; }
            public string Token { get; set; }
            public string Email { get; set; }
            public string Role { get; set; }
        }
    }
}
