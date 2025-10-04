using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizMasterServer.Services;
using System.Security.Claims;
using System.Text.Json;
using static QuizMasterServer.DTOs.AuthDtos;

namespace QuizMasterServer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthController(IAuthService authService, IJwtTokenService jwtTokenService)
        {
            _authService = authService;
            _jwtTokenService = jwtTokenService;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Login user
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary> 
        /// Start Google OAuth login
        /// </summary>
        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google-response", // נתיב אחר!
                IsPersistent = false
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Google OAuth response handler - זה ה-endpoint האמיתי שלנו
        /// </summary>
        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://quizmastersystem.netlify.app";

            try
            {
                Console.WriteLine("=== GoogleResponse Started ===");

                // קריאת המידע מה-Cookie שה-Google handler יצר
                var authenticateResult = await HttpContext.AuthenticateAsync("Cookies");

                if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
                {
                    Console.WriteLine("Authentication failed");
                    if (authenticateResult.Failure != null)
                    {
                        Console.WriteLine($"Failure: {authenticateResult.Failure.Message}");
                    }
                    return Redirect($"{frontendUrl}/auth-error?message=auth_failed");
                }

                Console.WriteLine("Authentication succeeded");

                var claims = authenticateResult.Principal.Claims.ToList();
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value;

                Console.WriteLine($"Email: {email}, Name: {name}");

                if (string.IsNullOrEmpty(email))
                {
                    Console.WriteLine("Missing email");
                    await HttpContext.SignOutAsync("Cookies");
                    return Redirect($"{frontendUrl}/auth-error?message=missing_email");
                }

                // בדיקה אם המשתמש קיים במערכת
                var existingUser = await _authService.GetUserByEmailAsync(email);

                object userInfo;

                if (existingUser != null)
                {
                    Console.WriteLine($"User exists: {existingUser.Id}");
                    var jwtToken = _jwtTokenService.GenerateToken(existingUser);

                    userInfo = new
                    {
                        Id = existingUser.Id,
                        Email = existingUser.Email,
                        Name = existingUser.Username ?? name,
                        Role = existingUser.Role,
                        Picture = picture,
                        Token = jwtToken
                    };
                }
                else
                {
                    Console.WriteLine("Creating new user");

                    var registerRequest = new RegisterRequest
                    {
                        Email = email,
                        Password = Guid.NewGuid().ToString(),
                        Role = "Student"
                    };

                    var newUserResponse = await _authService.RegisterAsync(registerRequest);
                    Console.WriteLine($"New user created: {newUserResponse.UserId}");

                    userInfo = new
                    {
                        Id = newUserResponse.UserId,
                        Email = email,
                        Name = name,
                        Role = "Student",
                        Picture = picture,
                        Token = newUserResponse.Token,
                        IsNewUser = true
                    };
                }

                // ניקוי Cookie
                await HttpContext.SignOutAsync("Cookies");

                var userJson = JsonSerializer.Serialize(userInfo);
                var encodedUser = Uri.EscapeDataString(userJson);
                var redirectUrl = $"{frontendUrl}/auth-success?user={encodedUser}";

                Console.WriteLine("Redirecting to frontend");
                Console.WriteLine("=== GoogleResponse Completed ===");

                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR ===");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");

                try { await HttpContext.SignOutAsync("Cookies"); } catch { }

                return Redirect($"{frontendUrl}/auth-error?message={Uri.EscapeDataString(ex.Message)}");
            }
        }

        /// <summary>
        /// Get current user info
        /// </summary>
        [HttpGet("user")]
        [Authorize]
        public IActionResult GetUser()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var email = User.FindFirstValue(ClaimTypes.Email);
                var role = User.FindFirstValue(ClaimTypes.Role);

                return Ok(new
                {
                    Id = userId,
                    Email = email,
                    Role = role,
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Could not retrieve user info", message = ex.Message });
            }
        }

        /// <summary>
        /// Logout
        /// </summary>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { message = "Logged out successfully" });
        }
    }
}