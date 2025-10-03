using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
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
            var authProps = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google-callback"
                //RedirectUri = Url.Action("GoogleCallback")
            };
            return Challenge(authProps, GoogleDefaults.AuthenticationScheme);
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
        Console.WriteLine($"Frontend URL: {frontendUrl}");

        var result = await HttpContext.AuthenticateAsync("GoogleAuth");
        if (!result.Succeeded)
        {
            Console.WriteLine("Authentication failed");
            return Redirect($"{frontendUrl}/auth-error?message=authentication_failed");
        }

        Console.WriteLine("Authentication succeeded");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);
        var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var picture = result.Principal.FindFirstValue("picture");

        Console.WriteLine($"Email: {email}, Name: {name}");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Missing user info");
            return Redirect($"{frontendUrl}/auth-error?message=missing_user_info");
        }

        var existingUser = await _authService.GetUserByEmailAsync(email);
        Console.WriteLine($"Existing user: {existingUser != null}");

        object userInfo;

        if (existingUser != null)
        {
            Console.WriteLine("User exists, generating token");
            var jwtToken = _jwtTokenService.GenerateToken(existingUser);
            userInfo = new
            {
                Id = existingUser.Id,
                Email = existingUser.Email,
                Name = existingUser.Username,
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
            Console.WriteLine("User registered successfully");
            
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
        var redirectUrl = $"{frontendUrl}/auth-success?user={Uri.EscapeDataString(userJson)}";
        
        Console.WriteLine($"Redirecting to: {redirectUrl}");
        return Redirect(redirectUrl);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"=== ERROR in GoogleCallback ===");
        Console.WriteLine($"Message: {ex.Message}");
        Console.WriteLine($"StackTrace: {ex.StackTrace}");
        
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        }
        
        return Redirect($"{frontendUrl}/auth-error?message={Uri.EscapeDataString(ex.Message)}");
    }
}

        /// <summary>
        /// Get current user info (works with JWT)
        /// </summary>
        [HttpGet("user")]
        [Authorize]
        public async Task<IActionResult> GetUser()
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
                    IsAuthenticated = User.Identity.IsAuthenticated
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Could not retrieve user info", message = ex.Message });
            }
        }

        /// <summary>
        /// Logout (clears Google auth cookie)
        /// </summary>
        [HttpPost("google-logout")]
        public async Task<IActionResult> GoogleLogout()
        {
            await HttpContext.SignOutAsync("GoogleAuth");
            return Ok(new { message = "Logged out successfully" });
        }
    }
}