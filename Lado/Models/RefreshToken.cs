namespace Lado.Models
{
    /// <summary>
    /// Token de refresco para autenticacion JWT en app movil
    /// </summary>
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }

        // Navegacion
        public virtual ApplicationUser? User { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiryDate;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
