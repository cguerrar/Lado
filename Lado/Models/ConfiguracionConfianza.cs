using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Configuración de los criterios para calcular el nivel de confianza de usuarios.
    /// Cada criterio otorga puntos hacia el nivel total (máximo 5 estrellas).
    /// </summary>
    public class ConfiguracionConfianza
    {
        [Key]
        public int Id { get; set; }

        // ========================================
        // CRITERIO 1: Verificación de Identidad
        // ========================================
        [Display(Name = "Verificación de Identidad Habilitada")]
        public bool VerificacionIdentidadHabilitada { get; set; } = true;

        [Display(Name = "Puntos por Verificación de Identidad")]
        [Range(0, 5)]
        public int PuntosVerificacionIdentidad { get; set; } = 1;

        [Display(Name = "Descripción Verificación Identidad")]
        [StringLength(200)]
        public string DescripcionVerificacionIdentidad { get; set; } = "Identidad verificada con documento";

        // ========================================
        // CRITERIO 2: Verificación de Edad
        // ========================================
        [Display(Name = "Verificación de Edad Habilitada")]
        public bool VerificacionEdadHabilitada { get; set; } = true;

        [Display(Name = "Puntos por Verificación de Edad")]
        [Range(0, 5)]
        public int PuntosVerificacionEdad { get; set; } = 1;

        [Display(Name = "Descripción Verificación Edad")]
        [StringLength(200)]
        public string DescripcionVerificacionEdad { get; set; } = "Edad verificada (+18)";

        // ========================================
        // CRITERIO 3: Tasa de Respuesta
        // ========================================
        [Display(Name = "Tasa de Respuesta Habilitada")]
        public bool TasaRespuestaHabilitada { get; set; } = true;

        [Display(Name = "Puntos por Tasa de Respuesta")]
        [Range(0, 5)]
        public int PuntosTasaRespuesta { get; set; } = 1;

        [Display(Name = "Porcentaje Mínimo de Respuesta")]
        [Range(0, 100)]
        public int PorcentajeMinimoRespuesta { get; set; } = 70;

        [Display(Name = "Descripción Tasa de Respuesta")]
        [StringLength(200)]
        public string DescripcionTasaRespuesta { get; set; } = "Alta tasa de respuesta a mensajes";

        // ========================================
        // CRITERIO 4: Actividad Reciente
        // ========================================
        [Display(Name = "Actividad Reciente Habilitada")]
        public bool ActividadRecienteHabilitada { get; set; } = true;

        [Display(Name = "Puntos por Actividad Reciente")]
        [Range(0, 5)]
        public int PuntosActividadReciente { get; set; } = 1;

        [Display(Name = "Horas Máximas de Inactividad")]
        [Range(1, 168)] // 1 hora a 1 semana
        public int HorasMaximasInactividad { get; set; } = 48;

        [Display(Name = "Descripción Actividad Reciente")]
        [StringLength(200)]
        public string DescripcionActividadReciente { get; set; } = "Usuario activo recientemente";

        // ========================================
        // CRITERIO 5: Contenido Publicado
        // ========================================
        [Display(Name = "Contenido Publicado Habilitado")]
        public bool ContenidoPublicadoHabilitado { get; set; } = true;

        [Display(Name = "Puntos por Contenido Publicado")]
        [Range(0, 5)]
        public int PuntosContenidoPublicado { get; set; } = 1;

        [Display(Name = "Mínimo de Publicaciones")]
        [Range(1, 100)]
        public int MinimoPublicaciones { get; set; } = 5;

        [Display(Name = "Descripción Contenido Publicado")]
        [StringLength(200)]
        public string DescripcionContenidoPublicado { get; set; } = "Creador con contenido consistente";

        // ========================================
        // CONFIGURACIÓN GENERAL
        // ========================================
        [Display(Name = "Nivel Máximo de Confianza")]
        [Range(1, 10)]
        public int NivelMaximo { get; set; } = 5;

        [Display(Name = "Mostrar Badges en Perfil")]
        public bool MostrarBadgesEnPerfil { get; set; } = true;

        [Display(Name = "Mostrar Estrellas en Perfil")]
        public bool MostrarEstrellasEnPerfil { get; set; } = true;

        // ========================================
        // AUDITORÍA
        // ========================================
        [Display(Name = "Fecha de Última Modificación")]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        [Display(Name = "Modificado Por")]
        [StringLength(100)]
        public string? ModificadoPor { get; set; }
    }
}
