using System.ComponentModel.DataAnnotations;

namespace Lado.DTOs.Auth
{
    /// <summary>
    /// Solicitud de login
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email invalido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Informacion del dispositivo (opcional)
        /// </summary>
        public string? DeviceInfo { get; set; }
    }

    /// <summary>
    /// Solicitud de refresh token
    /// </summary>
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "El refresh token es requerido")]
        public string RefreshToken { get; set; } = string.Empty;

        public string? DeviceInfo { get; set; }
    }

    /// <summary>
    /// Solicitud de login con Google
    /// </summary>
    public class GoogleLoginRequest
    {
        [Required(ErrorMessage = "El token de Google es requerido")]
        public string IdToken { get; set; } = string.Empty;

        public string? DeviceInfo { get; set; }
    }
}
