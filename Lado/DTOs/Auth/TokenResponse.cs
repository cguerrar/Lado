namespace Lado.DTOs.Auth
{
    /// <summary>
    /// Respuesta con tokens JWT
    /// </summary>
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
        public UserTokenInfo User { get; set; } = new();
    }

    /// <summary>
    /// Informacion basica del usuario en el token
    /// </summary>
    public class UserTokenInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public bool EsCreador { get; set; }
        public bool EstaVerificado { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
