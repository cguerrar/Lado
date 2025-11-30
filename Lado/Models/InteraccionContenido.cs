using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lado.Models
{
    [Index(nameof(UsuarioId), nameof(ContenidoId), nameof(TipoInteraccion))]
    [Index(nameof(ContenidoId), nameof(FechaInteraccion))]
    public class InteraccionContenido
    {
        [Key]
        public long Id { get; set; }

        public string? UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido? Contenido { get; set; }

        public TipoInteraccion TipoInteraccion { get; set; }

        public DateTime FechaInteraccion { get; set; } = DateTime.Now;

        // Para tracking de tiempo (vistas completas)
        public int? SegundosVisto { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(100)]
        public string? SessionId { get; set; }

        [StringLength(50)]
        public string? Dispositivo { get; set; }
    }
}
