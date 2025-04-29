using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using QuizMaster.DTO;
using QuizMaster.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BC = BCrypt.Net.BCrypt;

namespace QuizMaster.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IConfiguration _configuration;

        public UserService(IMongoDatabase database, IConfiguration configuration)
        {
            _users = database.GetCollection<User>("Users");
            _configuration = configuration;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _users.Find(user => true).ToListAsync();
        }

        public async Task<User> GetByIdAsync(string id)
        {
            return await _users.Find(user => user.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _users.Find(user => user.Email == email).FirstOrDefaultAsync();
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest registerRequest)
        {
            // Check if user already exists
            var existingUser = await GetByEmailAsync(registerRequest.Email);
            if (existingUser != null)
            {
                throw new Exception("User with this email already exists");
            }

            // Create new user
            var user = new User
            {
                Username = registerRequest.Username,
                Email = registerRequest.Email,
                PasswordHash = BC.HashPassword(registerRequest.Password),
                Role = registerRequest.Role,
                CreatedAt = DateTime.Now
            };

            await _users.InsertOneAsync(user);

            // Generate token
            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                Token = token,
                User = user
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest loginRequest)
        {
            var user = await GetByEmailAsync(loginRequest.Email);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            if (!BC.Verify(loginRequest.Password, user.PasswordHash))
            {
                throw new Exception("Invalid password");
            }

            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                Token = token,
                User = user
            };
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}