using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Métricas diarias de rendimiento de supervisores
    /// </summary>
    public class MetricaSupervisor
    {
        public int Id { get; set; }

        /// <summary>Supervisor</summary>
        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [ForeignKey("SupervisorId")]
        public virtual ApplicationUser Supervisor { get; set; } = null!;

        /// <summary>Fecha de las métricas (solo fecha, sin hora)</summary>
        [Column(TypeName = "date")]
        public DateTime Fecha { get; set; }

        // ═══════════════════════════════════════════════════════════
        // CONTADORES
        // ═══════════════════════════════════════════════════════════

        /// <summary>Total de items revisados</summary>
        public int TotalRevisados { get; set; } = 0;

        /// <summary>Items aprobados</summary>
        public int Aprobados { get; set; } = 0;

        /// <summary>Items rechazados</summary>
        public int Rechazados { get; set; } = 0;

        /// <summary>Items censurados</summary>
        public int Censurados { get; set; } = 0;

        /// <summary>Items escalados a admin</summary>
        public int Escalados { get; set; } = 0;

        /// <summary>Decisiones revertidas</summary>
        public int Revertidos { get; set; } = 0;

        // ═══════════════════════════════════════════════════════════
        // TIEMPOS
        // ═══════════════════════════════════════════════════════════

        /// <summary>Tiempo promedio de revisión en segundos</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal TiempoPromedioSegundos { get; set; } = 0;

        /// <summary>Tiempo total trabajado en segundos</summary>
        public int TiempoTotalSegundos { get; set; } = 0;

        /// <summary>Revisión más rápida (segundos)</summary>
        public int? TiempoMinimoSegundos { get; set; }

        /// <summary>Revisión más lenta (segundos)</summary>
        public int? TiempoMaximoSegundos { get; set; }

        // ═══════════════════════════════════════════════════════════
        // SESIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>Hora de inicio de actividad</summary>
        public DateTime? HoraInicioActividad { get; set; }

        /// <summary>Hora de última actividad</summary>
        public DateTime? HoraUltimaActividad { get; set; }

        /// <summary>Número de sesiones en el día</summary>
        public int NumeroSesiones { get; set; } = 0;

        // ═══════════════════════════════════════════════════════════
        // CALIDAD
        // ═══════════════════════════════════════════════════════════

        /// <summary>Tasa de aprobación (porcentaje)</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal TasaAprobacion { get; set; } = 0;

        /// <summary>Tasa de escalamiento (porcentaje)</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal TasaEscalamiento { get; set; } = 0;

        /// <summary>Concordancia con IA (porcentaje de acuerdo con clasificación IA)</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConcordanciaIA { get; set; }

        /// <summary>Fecha de última actualización</summary>
        public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resumen de métricas para un período
    /// </summary>
    public class ResumenMetricasSupervisor
    {
        public string SupervisorId { get; set; } = string.Empty;
        public string SupervisorNombre { get; set; } = string.Empty;
        public string? SupervisorAvatar { get; set; }
        public string RolNombre { get; set; } = string.Empty;
        public string RolColor { get; set; } = "#4682B4";

        public int TotalRevisados { get; set; }
        public int Aprobados { get; set; }
        public int Rechazados { get; set; }
        public int Escalados { get; set; }
        public decimal TasaAprobacion { get; set; }
        public decimal TiempoPromedioSegundos { get; set; }
        public int DiasActivos { get; set; }
        public decimal PromedioRevisadosPorDia { get; set; }

        public int Ranking { get; set; }
        public bool EstaActivo { get; set; }
        public DateTime? UltimaActividad { get; set; }
    }
}
