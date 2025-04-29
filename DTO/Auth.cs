using QuizMaster.Models;

namespace QuizMaster.DTO
{

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class AuthResponse
    {
        public string Token { get; set; }
        public User User { get; set; }
    }
}