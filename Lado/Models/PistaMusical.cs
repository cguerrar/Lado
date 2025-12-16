using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Representa una pista musical disponible en la biblioteca de música
    /// para usar en el creador de contenido Reels
    /// </summary>
    public class PistaMusical
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Artista { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Album { get; set; }

        [Required]
        [StringLength(100)]
        public string Genero { get; set; } = string.Empty;

        /// <summary>
        /// Duración en segundos
        /// </summary>
        public int Duracion { get; set; }

        /// <summary>
        /// Ruta del archivo de audio (relativa a wwwroot)
        /// </summary>
        [Required]
        [StringLength(500)]
        public string RutaArchivo { get; set; } = string.Empty;

        /// <summary>
        /// Ruta de la imagen de portada (relativa a wwwroot)
        /// </summary>
        [StringLength(500)]
        public string? RutaPortada { get; set; }

        /// <summary>
        /// Indica si la pista es royalty-free y puede usarse libremente
        /// </summary>
        public bool EsLibreDeRegalias { get; set; } = true;

        /// <summary>
        /// Número de veces que se ha usado esta pista
        /// </summary>
        public int ContadorUsos { get; set; } = 0;

        /// <summary>
        /// BPM (beats por minuto) para sincronización
        /// </summary>
        public int? Bpm { get; set; }

        /// <summary>
        /// Estado de energía: baja, media, alta
        /// </summary>
        [StringLength(20)]
        public string? Energia { get; set; }

        /// <summary>
        /// Estado de ánimo: alegre, triste, relajado, intenso, etc.
        /// </summary>
        [StringLength(50)]
        public string? EstadoAnimo { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Categorías de géneros musicales para el creador de Reels
    /// </summary>
    public static class GenerosMusica
    {
        public const string Pop = "Pop";
        public const string HipHop = "Hip Hop";
        public const string Electronica = "Electrónica";
        public const string Rock = "Rock";
        public const string Latino = "Latino";
        public const string Reggaeton = "Reggaetón";
        public const string RnB = "R&B";
        public const string Indie = "Indie";
        public const string Lofi = "Lo-Fi";
        public const string Cinematico = "Cinemático";
        public const string Ambiental = "Ambiental";
        public const string Acustico = "Acústico";

        public static List<string> Todos => new()
        {
            Pop, HipHop, Electronica, Rock, Latino,
            Reggaeton, RnB, Indie, Lofi, Cinematico,
            Ambiental, Acustico
        };
    }
}
