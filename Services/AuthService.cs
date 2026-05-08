using MadhubaniPaintingAPI.Data;
using MadhubaniPaintingAPI.DTOs;
using MadhubaniPaintingAPI.Entities;
using MadhubaniPaintingAPI.Utlities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace MadhubaniPaintingAPI.Services
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(RegisterRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        public Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
        public Task LogoutAsync(string refreshToken);
        public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request);
        public Task<string> ResetPasswordAsync(ResetPasswordRequest request);
    } 

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly Utilities _utilities = new Utilities();

        public AuthService(AppDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public async Task<string> RegisterAsync(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(x => x.Email == request.Email))
                throw new Exception("Email already exists");

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return "User registered successfully";
        }

        //public async Task<string> LoginAsync(LoginRequest request)
        //{
        //    var user = await _context.Users
        //        .FirstOrDefaultAsync(x => x.Email == request.Email);

        //    if (user == null)
        //        throw new Exception("Invalid email or password");

        //    bool isPasswordValid = BCrypt.Net.BCrypt
        //        .Verify(request.Password, user.PasswordHash);

        //    if (!isPasswordValid)
        //        throw new Exception("Invalid email or password");

        //    return _jwtService.GenerateToken(user);
        //}

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
                throw new Exception("Invalid email or password");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            bool isPasswordValid = BCrypt.Net.BCrypt
                .Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
                throw new Exception("Invalid email or password");

            var roles = user.UserRoles
                .Select(ur => ur.Role.Name)
                .ToList();

            var token = _jwtService.GenerateToken(user, roles);
            var accessToken = _jwtService.GenerateToken(user, roles);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var refreshTokenHash = _utilities.HashToken(refreshToken);

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                User = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Roles = roles
                }
            };
        }

        public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var incomingHash = _utilities.HashToken(request.RefreshToken);

            var storedToken = await _context.RefreshTokens
                .Include(r => r.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

            if (storedToken == null)
                throw new UnauthorizedAccessException("Invalid token");

            if (storedToken.IsRevoked)
            {
                // Token already used → possible attack
                throw new UnauthorizedAccessException("Token reuse detected");
            }

            if (storedToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Token expired");

            var user = storedToken.User;

            var roles = user.UserRoles.Select(r => r.Role.Name).ToList();

            // 🔥 Revoke old token
            storedToken.IsRevoked = true;

            // 🔥 Generate new tokens
            var newAccessToken = _jwtService.GenerateToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var newHash = _utilities.HashToken(newRefreshToken);

            //var newRefreshTokenEntity = new RefreshToken
            //{
            //    Id = Guid.NewGuid(),
            //    UserId = user.Id,
            //    Token = newRefreshToken,
            //    ExpiresAt = DateTime.UtcNow.AddDays(7),
            //    CreatedAt = DateTime.UtcNow
            //};

            storedToken.ReplacedByTokenHash = newHash;

            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(newRefreshTokenEntity);

            //var newAccessToken = _jwtService.GenerateToken(user, roles);

            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                User = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Roles = roles
                }
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var hash = _utilities.HashToken(refreshToken);

            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == hash);

            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync();
            }
            else
                throw new UnauthorizedAccessException("Invalid refresh token");
        }

        public async Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
                return "If email exists, reset link sent";

            var token = _jwtService.GenerateRefreshToken();
            var hash = _utilities.HashToken(token);

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow,
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // 🔥 TODO: Send Email (for now return token)
            return token;
        }

        public async Task<string> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var hash = _utilities.HashToken(request.Token);
            //var hash = request.Token;

            var resetToken = await _context.PasswordResetTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.TokenHash == hash);

            if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid or expired token");

            var user = resetToken.User;

            // 🔐 Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            // 🔥 Mark token used
            resetToken.IsUsed = true;

            await _context.SaveChangesAsync();

            return "Password reset successful";
        }
    }
}