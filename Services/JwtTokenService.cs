using QuizMasterServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuizMasterServer.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(User user)
        {
            // קריאה ממשתני סביבה או מ-appsettings
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
                         ?? _config["Jwt:Key"]
                         ?? throw new InvalidOperationException("JWT_KEY not configured");

            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                            ?? _config["Jwt:Issuer"]
                            ?? "https://quizmasterserver.onrender.com";

            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                              ?? _config["Jwt:Audience"]
                              ?? "QuizMasterClient";

            var expiresMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRES_MINUTES");
            var expires = int.TryParse(expiresMinutes, out var minutes)
                          ? minutes
                          : int.Parse(_config["Jwt:ExpiresMinutes"] ?? "60");

            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
                throw new InvalidOperationException("JWT_KEY must be at least 32 characters");

            var key = Encoding.UTF8.GetBytes(jwtKey);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Username ?? user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expires),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}