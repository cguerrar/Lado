namespace Lado.Models
{
    /// <summary>
    /// Token de acceso activo para validación en tiempo real
    /// Permite revocar tokens JWT antes de su expiración natural
    /// </summary>
    public class ActiveToken
    {
        public int Id { get; set; }

        /// <summary>
        /// JWT ID único (claim jti)
        /// </summary>
        public string Jti { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de expiración del token
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Token revocado manualmente (logout, cambio contraseña, etc.)
        /// </summary>
        public bool IsRevoked { get; set; }

        /// <summary>
        /// Información del dispositivo (User-Agent, etc.)
        /// </summary>
        public string? DeviceInfo { get; set; }

        /// <summary>
        /// Dirección IP del cliente
        /// </summary>
        public string? IpAddress { get; set; }

        // Navegación
        public virtual ApplicationUser? User { get; set; }

        // Propiedades calculadas
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
