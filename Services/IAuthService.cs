using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using System.Threading.Tasks;
using static QuizMasterServer.DTOs.AuthDtos;

namespace QuizMasterServer.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
    }
}