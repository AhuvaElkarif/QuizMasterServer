using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IConfiguration _configuration;

    public UserController(MongoDBService mongoDBService, IConfiguration configuration)
    {
        _usersCollection = mongoDBService.GetCollection<User>("Users");
        _configuration = configuration;
    }

    [HttpGet]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _usersCollection.Find(_ => true).ToListAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        // Remove password hash before returning
        user.PasswordHash = null;
        return Ok(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        // Check if email already exists
        var existingUser = await _usersCollection.Find(u => u.Email == model.Email).FirstOrDefaultAsync();
        if (existingUser != null)
            return BadRequest("User with this email already exists");

        // Create new user
        var user = new User
        {
            Name = model.Name,
            Email = model.Email,
            PasswordHash = HashPassword(model.Password),
            Role = model.Role
        };

        await _usersCollection.InsertOneAsync(user);

        // Remove password hash before returning
        user.PasswordHash = null;
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginModel model)
    {
        var user = await _usersCollection.Find(u => u.Email == model.Email).FirstOrDefaultAsync();

        if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password");

        var token = GenerateJwtToken(user);

        return Ok(new { token });
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, UpdateUserModel userIn)
    {
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        // Only allow updates to the user's own account or if user is a teacher
        string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId != id && userRole != "Teacher")
            return Forbid();

        // Update user properties
        user.Name = userIn.Name ?? user.Name;
        user.Email = userIn.Email ?? user.Email;

        if (!string.IsNullOrEmpty(userIn.Password))
            user.PasswordHash = HashPassword(userIn.Password);

        // Only teachers can change roles
        if (userRole == "Teacher" && !string.IsNullOrEmpty(userIn.Role))
            user.Role = userIn.Role;

        await _usersCollection.ReplaceOneAsync(u => u.Id == id, user);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await _usersCollection.DeleteOneAsync(u => u.Id == id);

        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }

    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(3),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// Models for request validation
public class RegisterModel
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string Role { get; set; } // "Teacher" or "Student"
}

public class LoginModel
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class UpdateUserModel
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string Role { get; set; }
}