using QuizMasterServer.Models;

namespace QuizMasterServer.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
    }
}
