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
        /// Google OAuth callback
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

                // בדיקה אם יש state וcode בפרמטרים
                if (!Request.Query.ContainsKey("state") || !Request.Query.ContainsKey("code"))
                {
                    Console.WriteLine("Missing state or code parameter - likely a redirect loop");
                    return Redirect($"{frontendUrl}/auth-error?message=invalid_callback");
                }

                Console.WriteLine($"Has State: True");
                Console.WriteLine($"Has Code: True");

                // אימות מול Google - נעשה דרך ה-Cookie scheme שהוא ה-SignInScheme
                var authenticateResult = await HttpContext.AuthenticateAsync("Cookies");

                if (!authenticateResult.Succeeded)
                {
                    Console.WriteLine($"Cookie Authentication Failed!");
                    Console.WriteLine($"Failure: {authenticateResult.Failure?.Message}");
                    return Redirect($"{frontendUrl}/auth-error?message=auth_failed");
                }

                Console.WriteLine("Authentication Succeeded!");

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
                    await HttpContext.SignOutAsync("Cookies");
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

                // חשוב מאוד! ניקוי ה-Cookie מיד אחרי שסיימנו
                await HttpContext.SignOutAsync("Cookies");

                var userJson = JsonSerializer.Serialize(userInfo);
                var encodedUser = Uri.EscapeDataString(userJson);
                var redirectUrl = $"{frontendUrl}/auth-success?user={encodedUser}";

                Console.WriteLine($"User data prepared, redirecting to frontend");
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

                // ניקוי Cookie גם במקרה של שגיאה
                try
                {
                    await HttpContext.SignOutAsync("Cookies");
                }
                catch { }

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