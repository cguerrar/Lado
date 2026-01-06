using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Representa una foto destacada en el PhotoWall (Muro)
    /// Los usuarios pueden pagar con LadoCoins para que su foto aparezca más grande
    /// </summary>
    public class FotoDestacada
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del contenido (foto) que se destaca
        /// </summary>
        [Required]
        public int ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido? Contenido { get; set; }

        /// <summary>
        /// ID del usuario que pagó por destacar
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Nivel de destacado (determina el tamaño)
        /// </summary>
        public NivelDestacado Nivel { get; set; } = NivelDestacado.Normal;

        /// <summary>
        /// Fecha/hora en que se activó el destacado
        /// </summary>
        public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha/hora en que expira el destacado
        /// </summary>
        public DateTime FechaExpiracion { get; set; }

        /// <summary>
        /// Cantidad de LadoCoins pagados
        /// </summary>
        public int CostoPagado { get; set; }

        /// <summary>
        /// Si el destacado está actualmente activo (no expirado)
        /// </summary>
        [NotMapped]
        public bool EstaActivo => DateTime.UtcNow >= FechaInicio && DateTime.UtcNow <= FechaExpiracion;

        /// <summary>
        /// Tiempo restante antes de que expire el destacado
        /// </summary>
        [NotMapped]
        public TimeSpan TiempoRestante => EstaActivo
            ? FechaExpiracion - DateTime.UtcNow
            : TimeSpan.Zero;

        /// <summary>
        /// Obtiene el tiempo restante formateado como texto legible
        /// </summary>
        [NotMapped]
        public string TiempoRestanteTexto
        {
            get
            {
                if (!EstaActivo) return "Expirado";

                var tiempo = TiempoRestante;
                if (tiempo.TotalDays >= 1)
                    return $"{(int)tiempo.TotalDays}d {tiempo.Hours}h";
                if (tiempo.TotalHours >= 1)
                    return $"{(int)tiempo.TotalHours}h {tiempo.Minutes}m";
                return $"{tiempo.Minutes}m";
            }
        }

        /// <summary>
        /// Obtiene el tamaño en píxeles según el nivel
        /// </summary>
        [NotMapped]
        public int TamanoPixeles => Nivel switch
        {
            NivelDestacado.Normal => 18,
            NivelDestacado.Bronce => 36,
            NivelDestacado.Plata => 54,
            NivelDestacado.Oro => 72,
            NivelDestacado.Diamante => 90,
            _ => 18
        };

        /// <summary>
        /// Obtiene el costo en LadoCoins según el nivel
        /// </summary>
        public static int ObtenerCosto(NivelDestacado nivel) => nivel switch
        {
            NivelDestacado.Normal => 0,
            NivelDestacado.Bronce => 5,
            NivelDestacado.Plata => 15,
            NivelDestacado.Oro => 30,
            NivelDestacado.Diamante => 50,
            _ => 0
        };

        /// <summary>
        /// Obtiene la duración en horas según el nivel
        /// </summary>
        public static int ObtenerDuracionHoras(NivelDestacado nivel) => nivel switch
        {
            NivelDestacado.Normal => 0,      // No tiene duración (es gratis)
            NivelDestacado.Bronce => 24,     // 1 día
            NivelDestacado.Plata => 48,      // 2 días
            NivelDestacado.Oro => 72,        // 3 días
            NivelDestacado.Diamante => 168,  // 1 semana
            _ => 0
        };
    }
}
