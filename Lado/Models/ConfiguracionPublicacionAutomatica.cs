using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Configuración de publicación automática para usuarios administrados.
    /// Define la frecuencia y horarios de publicación para simular actividad orgánica.
    /// </summary>
    public class ConfiguracionPublicacionAutomatica
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Usuario al que aplica esta configuración
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = null!;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; } = null!;

        /// <summary>
        /// Si la publicación automática está activa
        /// </summary>
        public bool Activo { get; set; } = false;

        /// <summary>
        /// Número mínimo de publicaciones por día
        /// </summary>
        [Range(0, 50)]
        public int PublicacionesMinPorDia { get; set; } = 1;

        /// <summary>
        /// Número máximo de publicaciones por día
        /// </summary>
        [Range(0, 50)]
        public int PublicacionesMaxPorDia { get; set; } = 3;

        /// <summary>
        /// Hora de inicio del rango de publicación (ej: 09:00)
        /// </summary>
        public TimeSpan HoraInicio { get; set; } = new TimeSpan(9, 0, 0);

        /// <summary>
        /// Hora de fin del rango de publicación (ej: 22:00)
        /// </summary>
        public TimeSpan HoraFin { get; set; } = new TimeSpan(22, 0, 0);

        /// <summary>
        /// Si se debe publicar en fines de semana
        /// </summary>
        public bool PublicarFinesDeSemana { get; set; } = true;

        /// <summary>
        /// Minutos de variación aleatoria (±) para simular comportamiento humano
        /// </summary>
        [Range(0, 120)]
        public int VariacionMinutos { get; set; } = 30;

        /// <summary>
        /// Tipo de lado por defecto para las publicaciones
        /// </summary>
        public TipoLado TipoLadoDefault { get; set; } = TipoLado.LadoA;

        /// <summary>
        /// Si por defecto el contenido es solo para suscriptores
        /// </summary>
        public bool SoloSuscriptoresDefault { get; set; } = false;

        /// <summary>
        /// Número mínimo de Stories por día (independiente de posts)
        /// </summary>
        [Range(0, 20)]
        public int StoriesMinPorDia { get; set; } = 0;

        /// <summary>
        /// Número máximo de Stories por día (independiente de posts)
        /// </summary>
        [Range(0, 20)]
        public int StoriesMaxPorDia { get; set; } = 2;

        /// <summary>
        /// Fecha de la última publicación automática
        /// </summary>
        public DateTime? UltimaPublicacion { get; set; }

        /// <summary>
        /// Número de publicaciones realizadas hoy
        /// </summary>
        public int PublicacionesHoy { get; set; } = 0;

        /// <summary>
        /// Fecha del último reset del contador diario
        /// </summary>
        public DateTime? FechaUltimoReset { get; set; }

        /// <summary>
        /// Próxima fecha/hora programada para publicación
        /// </summary>
        public DateTime? ProximaPublicacion { get; set; }

        /// <summary>
        /// Fecha de creación de la configuración
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Total de publicaciones automáticas realizadas
        /// </summary>
        public int TotalPublicaciones { get; set; } = 0;

        /// <summary>
        /// Días específicos de la semana para publicar (null = todos)
        /// Formato: "1,2,3,4,5" para Lun-Vie (DayOfWeek values)
        /// </summary>
        [StringLength(20)]
        public string? DiasPermitidos { get; set; }

        /// <summary>
        /// Calcula si se puede publicar en este momento según la configuración
        /// </summary>
        public bool PuedePublicarAhora()
        {
            if (!Activo) return false;

            var ahora = DateTime.Now;
            var horaActual = ahora.TimeOfDay;

            // Verificar rango horario
            if (horaActual < HoraInicio || horaActual > HoraFin)
                return false;

            // Verificar día de la semana
            if (!PublicarFinesDeSemana && (ahora.DayOfWeek == DayOfWeek.Saturday || ahora.DayOfWeek == DayOfWeek.Sunday))
                return false;

            // Verificar días específicos si están configurados
            if (!string.IsNullOrEmpty(DiasPermitidos))
            {
                var dias = DiasPermitidos.Split(',').Select(d => int.Parse(d.Trim())).ToList();
                if (!dias.Contains((int)ahora.DayOfWeek))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Obtiene el número de publicaciones objetivo para hoy (aleatorio entre min y max)
        /// </summary>
        public int ObtenerPublicacionesObjetivoHoy(Random? random = null)
        {
            random ??= new Random();
            return random.Next(PublicacionesMinPorDia, PublicacionesMaxPorDia + 1);
        }
    }
}
