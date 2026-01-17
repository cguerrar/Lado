using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Configuración del modo mantenimiento del sitio
    /// </summary>
    public class ModoMantenimiento
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Si el modo mantenimiento está activo ahora
        /// </summary>
        [Display(Name = "Activo")]
        public bool EstaActivo { get; set; } = false;

        /// <summary>
        /// Fecha/hora de inicio del mantenimiento (para programar)
        /// </summary>
        [Display(Name = "Inicio Programado")]
        public DateTime? FechaInicio { get; set; }

        /// <summary>
        /// Fecha/hora de fin estimado del mantenimiento
        /// </summary>
        [Display(Name = "Fin Estimado")]
        public DateTime? FechaFinEstimado { get; set; }

        /// <summary>
        /// Mensaje a mostrar a los usuarios durante el mantenimiento
        /// </summary>
        [Display(Name = "Mensaje")]
        [StringLength(1000)]
        public string Mensaje { get; set; } = "Estamos realizando mejoras en la plataforma. Volveremos pronto.";

        /// <summary>
        /// Título de la página de mantenimiento
        /// </summary>
        [Display(Name = "Título")]
        [StringLength(200)]
        public string Titulo { get; set; } = "Sitio en Mantenimiento";

        /// <summary>
        /// Si se debe mostrar cuenta regresiva hasta el fin estimado
        /// </summary>
        [Display(Name = "Mostrar Cuenta Regresiva")]
        public bool MostrarCuentaRegresiva { get; set; } = true;

        /// <summary>
        /// Permitir acceso a creadores verificados durante mantenimiento
        /// </summary>
        [Display(Name = "Permitir Creadores Verificados")]
        public bool PermitirCreadoresVerificados { get; set; } = false;

        /// <summary>
        /// Rutas que siempre están permitidas (separadas por coma)
        /// Ej: /status,/api/status
        /// </summary>
        [Display(Name = "Rutas Permitidas")]
        [StringLength(500)]
        public string? RutasPermitidas { get; set; } = "/Status,/Admin,/Account/Login,/Account/Logout";

        /// <summary>
        /// Admin que activó el mantenimiento
        /// </summary>
        public string? ActivadoPorId { get; set; }

        [ForeignKey("ActivadoPorId")]
        public ApplicationUser? ActivadoPor { get; set; }

        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Notificar a usuarios X minutos antes del mantenimiento programado
        /// </summary>
        [Display(Name = "Notificar con anticipación (minutos)")]
        public int NotificarMinutosAntes { get; set; } = 30;

        /// <summary>
        /// Si ya se envió la notificación previa
        /// </summary>
        public bool NotificacionPreviaEnviada { get; set; } = false;
    }

    /// <summary>
    /// Registro histórico de mantenimientos realizados
    /// </summary>
    public class HistorialMantenimiento
    {
        [Key]
        public int Id { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        [StringLength(200)]
        public string? Titulo { get; set; }

        [StringLength(1000)]
        public string? Mensaje { get; set; }

        public string? ActivadoPorId { get; set; }

        [ForeignKey("ActivadoPorId")]
        public ApplicationUser? ActivadoPor { get; set; }

        public string? DesactivadoPorId { get; set; }

        [ForeignKey("DesactivadoPorId")]
        public ApplicationUser? DesactivadoPor { get; set; }

        /// <summary>
        /// Duración en minutos
        /// </summary>
        public int? DuracionMinutos { get; set; }

        [StringLength(500)]
        public string? Notas { get; set; }
    }
}
