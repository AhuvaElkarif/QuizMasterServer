using QuizMasterServer.Data;
using QuizMasterServer.Models;
using MongoDB.Driver;
using static QuizMasterServer.DTOs.AuthDtos;

namespace QuizMasterServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMongoDbContext _db;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthService(IMongoDbContext db, IJwtTokenService jwtTokenService)
        {
            _db = db;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Email and password are required.");

            if (request.Role != "Teacher" && request.Role != "Student")
                throw new ArgumentException("Role must be either 'Teacher' or 'Student'.");

            var existingUser = await _db.Users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (existingUser != null)
                throw new InvalidOperationException("Email already exists.");

            User user;
            if (request.Role == "Teacher")
                user = new Teacher();
            else
                user = new Student();

            user.Id = MongoDB.Bson.ObjectId.GenerateNewId();
            user.Email = request.Email;
            user.SetPassword(request.Password);

            await _db.Users.InsertOneAsync(user);

            var token = _jwtTokenService.GenerateToken(user);

            return new AuthResponse()
            {
                Token = token,
                Email = user.Email,
                Role = user.Role
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Email and password are required.");

            var user = await _db.Users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();

            if (user == null || !user.VerifyPassword(request.Password))
                throw new UnauthorizedAccessException("Invalid email or password.");

            var token = _jwtTokenService.GenerateToken(user);

            return new AuthResponse()
            {
                Token = token,
                Email = user.Email,
                Role = user.Role
            };
        }

        // <summary>
        /// Get user by email - needed for Google OAuth
        /// </summary>
        public async Task<User> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error retrieving user: {ex.Message}");
            }
        }
    }
}