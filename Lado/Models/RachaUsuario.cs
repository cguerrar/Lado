using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Tracking de rachas de actividad y contadores diarios para premios.
    /// Los contadores se resetean a medianoche (UTC).
    /// </summary>
    public class RachaUsuario
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        // ========================================
        // RACHA DE DÍAS CONSECUTIVOS
        // ========================================

        /// <summary>
        /// Cantidad de días consecutivos con login premiado
        /// </summary>
        [Display(Name = "Racha Actual")]
        public int RachaActual { get; set; } = 0;

        /// <summary>
        /// Record personal de racha más larga
        /// </summary>
        [Display(Name = "Racha Máxima")]
        public int RachaMaxima { get; set; } = 0;

        /// <summary>
        /// Fecha del último login que recibió premio
        /// </summary>
        [Display(Name = "Último Login Premio")]
        public DateTime? UltimoLoginPremio { get; set; }

        // ========================================
        // CONTADORES DIARIOS
        // ========================================

        /// <summary>
        /// Cantidad de likes dados hoy
        /// </summary>
        [Display(Name = "Likes Hoy")]
        public int LikesHoy { get; set; } = 0;

        /// <summary>
        /// Cantidad de comentarios hechos hoy
        /// </summary>
        [Display(Name = "Comentarios Hoy")]
        public int ComentariosHoy { get; set; } = 0;

        /// <summary>
        /// Cantidad de contenidos subidos hoy
        /// </summary>
        [Display(Name = "Contenidos Hoy")]
        public int ContenidosHoy { get; set; } = 0;

        // ========================================
        // FLAGS DE PREMIOS DIARIOS
        // ========================================

        /// <summary>
        /// Si ya recibió premio por 5 likes hoy
        /// </summary>
        [Display(Name = "Premio 5 Likes Hoy")]
        public bool Premio5LikesHoy { get; set; } = false;

        /// <summary>
        /// Si ya recibió premio por 3 comentarios hoy
        /// </summary>
        [Display(Name = "Premio 3 Comentarios Hoy")]
        public bool Premio3ComentariosHoy { get; set; } = false;

        /// <summary>
        /// Si ya recibió premio por subir contenido hoy
        /// </summary>
        [Display(Name = "Premio Contenido Hoy")]
        public bool PremioContenidoHoy { get; set; } = false;

        /// <summary>
        /// Si ya recibió premio por login hoy
        /// </summary>
        [Display(Name = "Premio Login Hoy")]
        public bool PremioLoginHoy { get; set; } = false;

        // ========================================
        // CONTROL DE RESET
        // ========================================

        /// <summary>
        /// Fecha del último reset de contadores diarios (hora local de la plataforma)
        /// </summary>
        [Display(Name = "Fecha Reset")]
        public DateTime FechaReset { get; set; } = DateTime.Now.Date;

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        [Display(Name = "Fecha Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navegación
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        // ========================================
        // MÉTODOS HELPER
        // ========================================

        /// <summary>
        /// Verifica si los contadores necesitan reset (nuevo día en hora local)
        /// </summary>
        public bool NecesitaReset()
        {
            return FechaReset.Date < DateTime.Now.Date;
        }

        /// <summary>
        /// Verifica si los contadores necesitan reset comparando con una fecha específica
        /// </summary>
        public bool NecesitaReset(DateTime fechaLocal)
        {
            return FechaReset.Date < fechaLocal.Date;
        }

        /// <summary>
        /// Resetea todos los contadores diarios
        /// </summary>
        /// <param name="fechaLocal">Fecha local de la plataforma (no UTC)</param>
        public void ResetearContadores(DateTime fechaLocal)
        {
            LikesHoy = 0;
            ComentariosHoy = 0;
            ContenidosHoy = 0;
            Premio5LikesHoy = false;
            Premio3ComentariosHoy = false;
            PremioContenidoHoy = false;
            PremioLoginHoy = false;
            FechaReset = fechaLocal.Date;
        }

        /// <summary>
        /// Resetea todos los contadores diarios (usa DateTime.Now como fecha local)
        /// </summary>
        public void ResetearContadores()
        {
            ResetearContadores(DateTime.Now);
        }

        /// <summary>
        /// Verifica si la racha sigue activa (login en las últimas 48 horas)
        /// </summary>
        public bool RachaActiva()
        {
            if (!UltimoLoginPremio.HasValue) return false;
            // La racha se pierde si pasan más de 48 horas desde el último login premiado
            return (DateTime.Now - UltimoLoginPremio.Value).TotalHours <= 48;
        }
    }
}
