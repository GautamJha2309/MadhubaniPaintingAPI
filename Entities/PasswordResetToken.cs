namespace MadhubaniPaintingAPI.Entities
{
    public class PasswordResetToken
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public string TokenHash { get; set; } 

        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
