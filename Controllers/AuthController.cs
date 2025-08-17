using QuizMasterServer.Data;
using QuizMasterServer.DTOs;
using QuizMasterServer.Models;
using QuizMasterServer.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;
using static QuizMasterServer.DTOs.AuthDtos;

namespace QuizMasterServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMongoDbContext _db;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthController(IMongoDbContext db, IJwtTokenService jwtTokenService)
        {
            _db = db;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required.");

            if (request.Role != "Teacher" && request.Role != "Student")
                return BadRequest("Role must be either 'Teacher' or 'Student'.");

            var existingUser = await _db.Users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();
            if (existingUser != null)
                return Conflict("Username already exists.");

            User user;
            if (request.Role == "Teacher")
                user = new Teacher();
            else
                user = new Student();

            user.Id = MongoDB.Bson.ObjectId.GenerateNewId();
            user.Username = request.Username;
            user.SetPassword(request.Password);

            await _db.Users.InsertOneAsync(user);

            var token = _jwtTokenService.GenerateToken(user);

            return Ok(new AuthResponse()
            {
                Token = token,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required.");

            var user = await _db.Users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();

            if (user == null || !user.VerifyPassword(request.Password))
                return Unauthorized("Invalid username or password.");

            var token = _jwtTokenService.GenerateToken(user);

            return Ok(new AuthResponse()
            {
                Token = token,
                Username = user.Username,
                Role = user.Role
            });
        }
    }
}