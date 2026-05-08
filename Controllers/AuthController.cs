using MadhubaniPaintingAPI.Data;
using MadhubaniPaintingAPI.DTOs;
using MadhubaniPaintingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MadhubaniPaintingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _config;

        public AuthController(IAuthService service, AppDbContext context, IJwtService jwtService, IConfiguration config)
        {
            _service = service;
            _context = context;
            _jwtService = jwtService;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _service.RegisterAsync(request);
            return Ok(new { message = result });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var response = await _service.LoginAsync(request);
            return Ok(response);
        }

        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] TokenRequest request)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["JwtSettings:Key"]);

            try
            {
                var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = _config["JwtSettings:Issuer"],
                    ValidAudience = _config["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                }, out SecurityToken validatedToken);

                var userId = principal.FindFirst("userId")?.Value;
                var email = principal.FindFirst(ClaimTypes.Email)?.Value;

                var roles = principal.FindAll(ClaimTypes.Role)
                                     .Select(r => r.Value)
                                     .ToList();

                return Ok(new
                {
                    status = "valid",
                    user = new
                    {
                        id = userId,
                        email = email,
                        roles = roles
                    }
                });
            }
            catch
            {
                return Unauthorized(new { status = "invalid" });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(RefreshTokenRequest request)
        {
            var response = await _service.RefreshTokenAsync(request);
            return Ok(response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync();
            }

            //if (token != null)
            //{
            //    token.IsRevoked = true;
            //    await _context.SaveChangesAsync();
            //}
            await _service.LogoutAsync(request.RefreshToken);

            return Ok(new { message = "Logged out successfully" });
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            var token = await _service.ForgotPasswordAsync(request);

            return Ok(new
            {
                message = "Reset link sent",
                token = token // ⚠️ remove in production
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request);
            return Ok(new { message = result });
        }
    }
}