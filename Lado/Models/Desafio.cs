using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    public class Desafio
    {
        public int Id { get; set; }

        [Required]
        public string FanId { get; set; }
        public virtual ApplicationUser Fan { get; set; }

        // Para desafíos directos
        public string? CreadorObjetivoId { get; set; }
        public virtual ApplicationUser? CreadorObjetivo { get; set; }

        // Para desafío asignado (después de aceptar propuesta o desafío directo)
        public string? CreadorAsignadoId { get; set; }
        public virtual ApplicationUser? CreadorAsignado { get; set; }

        [Required]
        public TipoDesafio TipoDesafio { get; set; }

        [Required]
        [StringLength(100)]
        public string Titulo { get; set; }

        [Required]
        [StringLength(1000)]
        public string Descripcion { get; set; }

        [Required]
        public decimal Presupuesto { get; set; } = 10.00m;

        // Precio final (puede diferir del presupuesto si se negocia)
        public decimal? PrecioFinal { get; set; }

        [Required]
        public int DiasPlazoPlazo { get; set; } = 3;

        [Required]
        [StringLength(50)]
        public string Categoria { get; set; }

        [Required]
        public TipoContenidoDesafio TipoContenido { get; set; }

        [Required]
        public VisibilidadDesafio Visibilidad { get; set; }

        [Required]
        public EstadoDesafio Estado { get; set; } = EstadoDesafio.Pendiente;

        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaExpiracion { get; set; }

        public DateTime? FechaAsignacion { get; set; }

        public DateTime? FechaCompletado { get; set; }

        public string? StripePaymentIntentId { get; set; }

        [Required]
        public EstadoPago EstadoPago { get; set; } = EstadoPago.Hold;

        // Ruta del contenido entregado
        public string? RutaContenido { get; set; }

        // Notas del creador al entregar
        public string? NotasCreador { get; set; }

        public int? Rating { get; set; }

        public string? ComentarioRating { get; set; }

        // Relaciones
        public virtual ICollection<PropuestaDesafio> Propuestas { get; set; } = new List<PropuestaDesafio>();

        public string? ArchivoEntregaUrl { get; set; }

        public DateTime? FechaEntrega { get; set; }

    }


    public class PropuestaDesafio
    {
        public int Id { get; set; }

        [Required]
        public int DesafioId { get; set; }
        public virtual Desafio Desafio { get; set; }

        [Required]
        public string CreadorId { get; set; }
        public virtual ApplicationUser Creador { get; set; }

        [Required]
        public decimal PrecioPropuesto { get; set; }

        [Required]
        public int DiasEntrega { get; set; }

        [Required]
        [StringLength(500)]
        public string MensajePropuesta { get; set; }

        public string? UrlsPortfolio { get; set; }

        [Required]
        public EstadoPropuesta Estado { get; set; } = EstadoPropuesta.Pendiente;

        [Required]
        public DateTime FechaPropuesta { get; set; } = DateTime.Now;

        public DateTime? FechaRespuesta { get; set; }
    }

    // Enums
    public enum TipoDesafio
    {
        Directo = 0,
        Publico = 1
    }

    public enum TipoContenidoDesafio
    {
        Imagen = 0,
        Video = 1,
        Audio = 2,
        Texto = 3
    }

    public enum VisibilidadDesafio
    {
        Privado = 0,
        Publico = 1
    }

    public enum EstadoDesafio
    {
        Pendiente = 0,              // Esperando aceptación (directo) o propuestas (público)
        RecibiendoPropuestas = 1,   // Recibiendo propuestas (público)
        Asignado = 2,               // Asignado a un creador
        EnProgreso = 3,             // En proceso de creación
        ContenidoSubido = 4,        // Contenido entregado, esperando aprobación
        Completado = 5,             // Completado y pagado
        Rechazado = 6,              // Rechazado por el creador (directo)
        Cancelado = 7,              // Cancelado por el fan
        Expirado = 8                // Expiró sin propuestas/aceptación
    }

    public enum EstadoPropuesta
    {
        Pendiente = 0,
        Aceptada = 1,
        Rechazada = 2
    }

    public enum EstadoPago
    {
        Hold = 0,
        Procesando = 1,
        Completado = 2,
        Reembolsado = 3,
        Liberado = 4
    }
}