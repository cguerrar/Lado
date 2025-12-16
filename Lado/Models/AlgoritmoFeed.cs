using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    public class AlgoritmoFeed
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty; // "cronologico", "trending", "para_ti", "seguidos"

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty; // "Cronológico", "Trending", etc.

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        [MaxLength(100)]
        public string? Icono { get; set; } // Nombre del icono SVG

        public bool Activo { get; set; } = true;

        public bool EsPorDefecto { get; set; } = false;

        public int Orden { get; set; } = 0;

        // Configuración flexible en JSON
        public string? ConfiguracionJson { get; set; }

        // Métricas de uso
        public int TotalUsos { get; set; } = 0;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaModificacion { get; set; }
    }

    // Modelo para la preferencia de algoritmo del usuario
    public class PreferenciaAlgoritmoUsuario
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        public ApplicationUser? Usuario { get; set; }

        public int AlgoritmoFeedId { get; set; }

        public AlgoritmoFeed? AlgoritmoFeed { get; set; }

        public DateTime FechaSeleccion { get; set; } = DateTime.Now;
    }

    // Configuración de algoritmos (deserializada desde JSON)
    public class ConfiguracionAlgoritmo
    {
        // Trending
        public double PesoLikes { get; set; } = 1.0;
        public double PesoComentarios { get; set; } = 3.0;
        public double PesoVistas { get; set; } = 0.1;
        public double PesoCompartidos { get; set; } = 3.0;
        public int HorasDecay { get; set; } = 24;

        // Para Ti
        public double PesoHistorial { get; set; } = 0.6;
        public double PesoSimilitud { get; set; } = 0.4;

        // Seguidos Primero
        public double PorcentajeSeguidosInicial { get; set; } = 70;

        // Descubrimiento
        public int MinEngagement { get; set; } = 5;
        public int MaxSeguidores { get; set; } = 10000;
    }
}
