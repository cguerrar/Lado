using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Tipo de destinatarios para la campaña
    /// </summary>
    public enum TipoDestinatarioEmail
    {
        /// <summary>Todos los usuarios con email válido</summary>
        Todos = 0,
        /// <summary>Solo usuarios creadores</summary>
        Creadores = 1,
        /// <summary>Solo fans (no creadores)</summary>
        Fans = 2,
        /// <summary>Usuarios activos en los últimos 30 días</summary>
        Activos = 3,
        /// <summary>Lista específica de emails</summary>
        EmailsEspecificos = 4
    }

    /// <summary>
    /// Estado de la campaña de email
    /// </summary>
    public enum EstadoCampanaEmail
    {
        /// <summary>En edición, no enviada</summary>
        Borrador = 0,
        /// <summary>Programada para envío futuro</summary>
        Programada = 1,
        /// <summary>Enviándose actualmente</summary>
        EnProgreso = 2,
        /// <summary>Envío completado</summary>
        Enviada = 3,
        /// <summary>Cancelada por el admin</summary>
        Cancelada = 4
    }

    /// <summary>
    /// Campaña de email masivo
    /// </summary>
    public class CampanaEmail
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre identificador de la campaña
        /// </summary>
        [Required]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Plantilla usada (opcional)
        /// </summary>
        public int? PlantillaId { get; set; }

        [ForeignKey("PlantillaId")]
        public PlantillaEmail? Plantilla { get; set; }

        /// <summary>
        /// Asunto del email
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Asunto { get; set; } = string.Empty;

        /// <summary>
        /// Contenido HTML del email
        /// </summary>
        [Required]
        public string ContenidoHtml { get; set; } = string.Empty;

        // ========================================
        // SEGMENTACIÓN
        // ========================================

        /// <summary>
        /// Tipo de destinatarios
        /// </summary>
        public TipoDestinatarioEmail TipoDestinatario { get; set; } = TipoDestinatarioEmail.Todos;

        /// <summary>
        /// Lista de emails específicos (solo si TipoDestinatario = EmailsEspecificos)
        /// Almacenado como JSON array
        /// </summary>
        public string? EmailsEspecificos { get; set; }

        /// <summary>
        /// Filtros adicionales en formato JSON
        /// </summary>
        public string? FiltroAdicional { get; set; }

        // ========================================
        // ESTADO Y FECHAS
        // ========================================

        /// <summary>
        /// Estado actual de la campaña
        /// </summary>
        public EstadoCampanaEmail Estado { get; set; } = EstadoCampanaEmail.Borrador;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha programada para envío (opcional)
        /// </summary>
        public DateTime? FechaProgramada { get; set; }

        /// <summary>
        /// Fecha cuando se inició el envío
        /// </summary>
        public DateTime? FechaInicioEnvio { get; set; }

        /// <summary>
        /// Fecha cuando terminó el envío
        /// </summary>
        public DateTime? FechaFinEnvio { get; set; }

        // ========================================
        // ESTADÍSTICAS
        // ========================================

        /// <summary>
        /// Total de destinatarios calculados
        /// </summary>
        public int TotalDestinatarios { get; set; }

        /// <summary>
        /// Emails enviados exitosamente
        /// </summary>
        public int Enviados { get; set; }

        /// <summary>
        /// Emails que fallaron
        /// </summary>
        public int Fallidos { get; set; }

        /// <summary>
        /// Detalle de errores en formato JSON
        /// </summary>
        public string? DetalleErrores { get; set; }

        // ========================================
        // AUDITORÍA
        // ========================================

        /// <summary>
        /// Admin que creó la campaña
        /// </summary>
        public string? CreadoPorId { get; set; }

        [ForeignKey("CreadoPorId")]
        public ApplicationUser? CreadoPor { get; set; }

        // ========================================
        // HELPERS
        // ========================================

        /// <summary>
        /// Porcentaje de progreso del envío
        /// </summary>
        [NotMapped]
        public double PorcentajeProgreso => TotalDestinatarios > 0
            ? Math.Round((double)(Enviados + Fallidos) / TotalDestinatarios * 100, 1)
            : 0;

        /// <summary>
        /// Tasa de éxito del envío
        /// </summary>
        [NotMapped]
        public double TasaExito => (Enviados + Fallidos) > 0
            ? Math.Round((double)Enviados / (Enviados + Fallidos) * 100, 1)
            : 0;

        /// <summary>
        /// Descripción legible del tipo de destinatario
        /// </summary>
        [NotMapped]
        public string TipoDestinatarioDescripcion => TipoDestinatario switch
        {
            TipoDestinatarioEmail.Todos => "Todos los usuarios",
            TipoDestinatarioEmail.Creadores => "Solo creadores",
            TipoDestinatarioEmail.Fans => "Solo fans",
            TipoDestinatarioEmail.Activos => "Usuarios activos (últimos 30 días)",
            TipoDestinatarioEmail.EmailsEspecificos => "Lista específica",
            _ => "Desconocido"
        };

        /// <summary>
        /// Color del badge según estado
        /// </summary>
        [NotMapped]
        public string EstadoBadgeClass => Estado switch
        {
            EstadoCampanaEmail.Borrador => "bg-secondary",
            EstadoCampanaEmail.Programada => "bg-info",
            EstadoCampanaEmail.EnProgreso => "bg-warning",
            EstadoCampanaEmail.Enviada => "bg-success",
            EstadoCampanaEmail.Cancelada => "bg-danger",
            _ => "bg-secondary"
        };
    }
}
