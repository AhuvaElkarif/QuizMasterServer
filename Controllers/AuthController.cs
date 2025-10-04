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
        public IActionResult GoogleLogin([FromQuery] string returnUrl = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback)),
                Items =
                {
                    { "returnUrl", returnUrl ?? "/" }
                }
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Google OAuth callback - גרסה מפושטת ללא Cookie dependency
        /// </summary>
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://quizmastersystem.netlify.app";

            try
            {
                Console.WriteLine("=== GoogleCallback Started ===");
                Console.WriteLine($"Request Path: {Request.Path}");
                Console.WriteLine($"Query String: {Request.QueryString}");
                Console.WriteLine($"Has State: {Request.Query.ContainsKey("state")}");
                Console.WriteLine($"Has Code: {Request.Query.ContainsKey("code")}");

                // ניסיון לאמת מול Google דרך ה-Cookie scheme
                var authenticateResult = await HttpContext.AuthenticateAsync("Cookies");

                if (!authenticateResult.Succeeded)
                {
                    Console.WriteLine($"Google Authentication Failed!");
                    Console.WriteLine($"Failure: {authenticateResult.Failure?.Message}");
                    return Redirect($"{frontendUrl}/auth-error?message=google_auth_failed");
                }

                Console.WriteLine("Google Authentication Succeeded!");

                var claims = authenticateResult.Principal.Claims;
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var googleId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value;

                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Name: {name}");
                Console.WriteLine($"GoogleId: {googleId}");

                if (string.IsNullOrEmpty(email))
                {
                    Console.WriteLine("Missing email claim");
                    return Redirect($"{frontendUrl}/auth-error?message=missing_email");
                }

                // בדיקה אם המשתמש קיים
                var existingUser = await _authService.GetUserByEmailAsync(email);

                object userInfo;

                if (existingUser != null)
                {
                    Console.WriteLine($"Existing user found: {existingUser.Id}");
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
                        Password = Guid.NewGuid().ToString(), // סיסמה אקראית
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

                var userJson = JsonSerializer.Serialize(userInfo);
                var encodedUser = Uri.EscapeDataString(userJson);
                var redirectUrl = $"{frontendUrl}/auth-success?user={encodedUser}";

                Console.WriteLine($"Redirecting to frontend: {redirectUrl.Substring(0, Math.Min(100, redirectUrl.Length))}...");
                Console.WriteLine("=== GoogleCallback Completed Successfully ===");

                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR in GoogleCallback ===");
                Console.WriteLine($"Type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                var errorMessage = Uri.EscapeDataString(ex.Message);
                return Redirect($"{frontendUrl}/auth-error?message={errorMessage}");
            }
        }

        /// <summary>
        /// Get current user info (works with JWT)
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