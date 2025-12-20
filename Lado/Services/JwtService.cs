using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Lado.Data;
using Lado.Models;
using Lado.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Lado.Services
{
    public interface IJwtService
    {
        Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, IList<string> roles, string? deviceInfo = null, string? ipAddress = null);
        Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string? deviceInfo = null, string? ipAddress = null);
        Task RevokeTokenAsync(string refreshToken);
        Task RevokeAllUserTokensAsync(string userId);
        Task RevokeAccessTokenAsync(string jti);
        Task RevokeAllAccessTokensAsync(string userId);
        Task<bool> IsTokenActiveAsync(string jti, string userId, int securityVersion);
        Task IncrementSecurityVersionAsync(string userId);
        ClaimsPrincipal? ValidateToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JwtService> _logger;
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _expiryMinutes;
        private readonly int _refreshTokenExpiryDays;

        public JwtService(
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;

            _jwtKey = _configuration["Jwt:Key"] ?? "LadoApp_DefaultKey_ChangeInProduction123!";
            _jwtIssuer = _configuration["Jwt:Issuer"] ?? "LadoApp";
            _jwtAudience = _configuration["Jwt:Audience"] ?? "LadoAppMobile";
            _expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 15;
            _refreshTokenExpiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var ref_exp) ? ref_exp : 30;
        }

        public async Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, IList<string> roles, string? deviceInfo = null, string? ipAddress = null)
        {
            var jti = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.AddMinutes(_expiryMinutes);

            // Generar access token con Jti y SecurityVersion
            var accessToken = GenerateAccessToken(user, roles, jti);

            // Guardar token activo en BD para validación en tiempo real
            await CreateActiveTokenAsync(jti, user.Id, expiresAt, deviceInfo, ipAddress);

            // Generar refresh token
            var refreshToken = await CreateRefreshTokenAsync(user.Id, deviceInfo, ipAddress);

            _logger.LogInformation("JWT generado para usuario {UserId} ({UserName}) - Jti: {Jti}", user.Id, user.UserName, jti[..8] + "...");

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = expiresAt,
                RefreshTokenExpiresAt = refreshToken.ExpiryDate,
                User = new UserTokenInfo
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    NombreCompleto = user.NombreCompleto ?? "",
                    FotoPerfil = user.FotoPerfil,
                    EsCreador = user.EsCreador,
                    EstaVerificado = user.CreadorVerificado,
                    Roles = roles.ToList()
                }
            };
        }

        public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string? deviceInfo = null, string? ipAddress = null)
        {
            var storedToken = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (storedToken == null)
            {
                _logger.LogWarning("Refresh token no encontrado: {Token}", refreshToken[..Math.Min(10, refreshToken.Length)] + "...");
                return null;
            }

            if (!storedToken.IsActive)
            {
                _logger.LogWarning("Refresh token inactivo para usuario {UserId}", storedToken.UserId);
                return null;
            }

            if (storedToken.User == null || !storedToken.User.EstaActivo)
            {
                _logger.LogWarning("Usuario inactivo o no encontrado para refresh token");
                return null;
            }

            // Revocar el refresh token actual
            storedToken.IsRevoked = true;
            await _context.SaveChangesAsync();

            // Generar nuevos tokens
            var roles = await GetUserRolesAsync(storedToken.User);
            return await GenerateTokensAsync(storedToken.User, roles, deviceInfo, ipAddress);
        }

        public async Task RevokeTokenAsync(string refreshToken)
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (storedToken != null)
            {
                storedToken.IsRevoked = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Refresh token revocado para usuario {UserId}", storedToken.UserId);
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId)
        {
            // Revocar todos los refresh tokens
            var refreshTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsRevoked = true;
            }

            // Revocar todos los access tokens activos
            var activeTokens = await _context.ActiveTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Todos los tokens revocados para usuario {UserId} (Refresh: {RefreshCount}, Access: {AccessCount})",
                userId, refreshTokens.Count, activeTokens.Count);
        }

        /// <summary>
        /// Revoca un access token específico por su Jti
        /// </summary>
        public async Task RevokeAccessTokenAsync(string jti)
        {
            var activeToken = await _context.ActiveTokens
                .FirstOrDefaultAsync(t => t.Jti == jti);

            if (activeToken != null)
            {
                activeToken.IsRevoked = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Access token revocado - Jti: {Jti}", jti[..Math.Min(8, jti.Length)] + "...");
            }
        }

        /// <summary>
        /// Revoca todos los access tokens activos de un usuario
        /// </summary>
        public async Task RevokeAllAccessTokensAsync(string userId)
        {
            var activeTokens = await _context.ActiveTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Todos los access tokens revocados para usuario {UserId} ({Count} tokens)",
                userId, activeTokens.Count);
        }

        /// <summary>
        /// Verifica si un token está activo (no revocado, no expirado, SecurityVersion válido)
        /// </summary>
        public async Task<bool> IsTokenActiveAsync(string jti, string userId, int securityVersion)
        {
            // Buscar el token activo
            var activeToken = await _context.ActiveTokens
                .FirstOrDefaultAsync(t => t.Jti == jti && t.UserId == userId);

            if (activeToken == null)
            {
                _logger.LogWarning("Token no encontrado en ActiveTokens - Jti: {Jti}", jti[..Math.Min(8, jti.Length)] + "...");
                return false;
            }

            if (!activeToken.IsActive)
            {
                _logger.LogWarning("Token inactivo (revocado o expirado) - Jti: {Jti}", jti[..Math.Min(8, jti.Length)] + "...");
                return false;
            }

            // Verificar SecurityVersion del usuario
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Usuario no encontrado para validación de token - UserId: {UserId}", userId);
                return false;
            }

            if (user.SecurityVersion != securityVersion)
            {
                _logger.LogWarning("SecurityVersion no coincide - Token: {TokenVersion}, Usuario: {UserVersion}",
                    securityVersion, user.SecurityVersion);
                return false;
            }

            if (!user.EstaActivo)
            {
                _logger.LogWarning("Usuario desactivado - UserId: {UserId}", userId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Incrementa el SecurityVersion del usuario, invalidando todos sus tokens
        /// </summary>
        public async Task IncrementSecurityVersionAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.SecurityVersion++;
                await _context.SaveChangesAsync();
                _logger.LogInformation("SecurityVersion incrementado para usuario {UserId} - Nueva versión: {Version}",
                    userId, user.SecurityVersion);
            }
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validando token JWT");
                return null;
            }
        }

        private string GenerateAccessToken(ApplicationUser user, IList<string> roles, string jti)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? ""),
                new(ClaimTypes.Email, user.Email ?? ""),
                new("nombre_completo", user.NombreCompleto ?? ""),
                new("es_creador", user.EsCreador.ToString().ToLower()),
                new("verificado", user.CreadorVerificado.ToString().ToLower()),
                new(JwtRegisteredClaimNames.Jti, jti),
                // Nuevo claim para validación de seguridad
                new("security_version", user.SecurityVersion.ToString())
            };

            // Agregar roles
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task CreateActiveTokenAsync(string jti, string userId, DateTime expiresAt, string? deviceInfo, string? ipAddress)
        {
            var activeToken = new ActiveToken
            {
                Jti = jti,
                UserId = userId,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress
            };

            _context.ActiveTokens.Add(activeToken);
            await _context.SaveChangesAsync();
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(string userId, string? deviceInfo, string? ipAddress)
        {
            var token = GenerateRefreshTokenString();

            var refreshToken = new RefreshToken
            {
                Token = token,
                UserId = userId,
                ExpiryDate = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow,
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        private static string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<IList<string>> GetUserRolesAsync(ApplicationUser user)
        {
            // Obtener roles del usuario desde la tabla de roles de Identity
            var roleIds = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var roles = await _context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync();

            return roles;
        }
    }
}
