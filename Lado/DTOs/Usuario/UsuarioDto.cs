namespace Lado.DTOs.Usuario
{
    /// <summary>
    /// DTO basico de usuario (para listas, referencias)
    /// </summary>
    public class UsuarioDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public bool EsCreador { get; set; }
        public bool EstaVerificado { get; set; }
    }

    /// <summary>
    /// DTO de perfil completo
    /// </summary>
    public class PerfilDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Biografia { get; set; }
        public string? FotoPerfil { get; set; }
        public string? FotoPortada { get; set; }
        public bool EsCreador { get; set; }
        public bool EstaVerificado { get; set; }

        // Estadisticas
        public int TotalPublicaciones { get; set; }
        public int TotalSuscriptores { get; set; }
        public int TotalSuscripciones { get; set; }

        // Info suscripcion (si es creador)
        public decimal? PrecioSuscripcion { get; set; }

        // Estado de relacion con el usuario actual
        public bool EstaSuscrito { get; set; }
        public bool EsBloqueado { get; set; }
        public bool MeBloqueo { get; set; }
        public bool EsMiPerfil { get; set; }

        // LadoPreferido
        public string? LadoPreferido { get; set; } // "LadoA", "LadoB"

        public DateTime FechaRegistro { get; set; }
    }

    /// <summary>
    /// DTO para editar perfil
    /// </summary>
    public class EditarPerfilRequest
    {
        public string? NombreCompleto { get; set; }
        public string? Biografia { get; set; }
        public string? LadoPreferido { get; set; } // "LadoA" o "LadoB"
    }

    /// <summary>
    /// DTO para creador en busquedas/listas
    /// </summary>
    public class CreadorDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public string? FotoPortada { get; set; }
        public bool EstaVerificado { get; set; }
        public decimal? PrecioSuscripcion { get; set; }
        public int TotalSuscriptores { get; set; }
        public int TotalPublicaciones { get; set; }
        public string? Biografia { get; set; }
        public bool EstaSuscrito { get; set; }
    }
}
