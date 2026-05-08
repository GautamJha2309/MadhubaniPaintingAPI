using MadhubaniPaintingAPI.Data;
using MadhubaniPaintingAPI.DTOs.Users;
using Microsoft.EntityFrameworkCore;

namespace MadhubaniPaintingAPI.Services
{
    public interface IUserService
    {
        Task<UserProfileDto> GetUserByIdAsync(Guid userId);
        Task UpdateUserAsync(Guid userId, UpdateUserRequest request);
    }
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileDto> GetUserByIdAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new Exception("User not found");

            return new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = user.UserRoles.Select(r => r.Role.Name).ToList()
            };
        }

        public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new Exception("User not found");

            user.FullName = request.FullName;

            await _context.SaveChangesAsync();
        }
    }
}
