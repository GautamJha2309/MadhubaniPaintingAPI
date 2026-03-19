using MadhubaniPaintingAPI.Data;
using MadhubaniPaintingAPI.DTOs;
using MadhubaniPaintingAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace MadhubaniPaintingAPI.Services
{
    public interface IUserService
    {
        Task<string> RegisterAsync(RegisterRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        public Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
    } 

    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;

        public UserService(AppDbContext context, IJwtService jwtService)
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

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
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
            var storedToken = await _context.RefreshTokens
                .Include(r => r.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

            if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid refresh token");

            var user = storedToken.User;

            var roles = user.UserRoles.Select(r => r.Role.Name).ToList();

            // 🔥 Revoke old token
            storedToken.IsRevoked = true;

            // 🔥 Generate new tokens
            var newAccessToken = _jwtService.GenerateToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(newRefreshTokenEntity);

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
    }
}