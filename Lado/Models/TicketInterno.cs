using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Ticket interno para comunicación entre moderadores/admins
    /// </summary>
    public class TicketInterno
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; } = "";

        [Required]
        public string Descripcion { get; set; } = "";

        public CategoriaTicket Categoria { get; set; } = CategoriaTicket.General;

        public PrioridadTicket Prioridad { get; set; } = PrioridadTicket.Normal;

        public EstadoTicket Estado { get; set; } = EstadoTicket.Abierto;

        /// <summary>
        /// Admin/Supervisor que creó el ticket
        /// </summary>
        [Required]
        public string CreadoPorId { get; set; } = "";

        [ForeignKey("CreadoPorId")]
        public ApplicationUser? CreadoPor { get; set; }

        /// <summary>
        /// Admin/Supervisor asignado al ticket
        /// </summary>
        public string? AsignadoAId { get; set; }

        [ForeignKey("AsignadoAId")]
        public ApplicationUser? AsignadoA { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime? FechaActualizacion { get; set; }

        public DateTime? FechaCierre { get; set; }

        /// <summary>
        /// ID del item relacionado (opcional)
        /// </summary>
        public int? ItemRelacionadoId { get; set; }

        /// <summary>
        /// Tipo del item relacionado (Reporte, Usuario, Contenido, etc.)
        /// </summary>
        public TipoItemTicket? TipoItemRelacionado { get; set; }

        /// <summary>
        /// Etiquetas separadas por coma
        /// </summary>
        [MaxLength(500)]
        public string? Etiquetas { get; set; }

        /// <summary>
        /// Respuestas/comentarios del ticket
        /// </summary>
        public ICollection<RespuestaTicket> Respuestas { get; set; } = new List<RespuestaTicket>();
    }

    /// <summary>
    /// Respuesta/comentario en un ticket
    /// </summary>
    public class RespuestaTicket
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        [ForeignKey("TicketId")]
        public TicketInterno? Ticket { get; set; }

        [Required]
        public string Contenido { get; set; } = "";

        [Required]
        public string AutorId { get; set; } = "";

        [ForeignKey("AutorId")]
        public ApplicationUser? Autor { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Si es una nota interna (solo visible para admins)
        /// </summary>
        public bool EsNotaInterna { get; set; } = false;
    }

    public enum CategoriaTicket
    {
        General = 0,
        Tecnico = 1,
        Moderacion = 2,
        Finanzas = 3,
        Legal = 4,
        Sugerencia = 5,
        Bug = 6,
        Urgente = 7
    }

    public enum PrioridadTicket
    {
        Baja = 0,
        Normal = 1,
        Alta = 2,
        Critica = 3
    }

    public enum EstadoTicket
    {
        Abierto = 0,
        EnProgreso = 1,
        EnEspera = 2,
        Resuelto = 3,
        Cerrado = 4
    }

    public enum TipoItemTicket
    {
        Usuario = 0,
        Contenido = 1,
        Reporte = 2,
        Apelacion = 3,
        Transaccion = 4,
        Otro = 99
    }
}
