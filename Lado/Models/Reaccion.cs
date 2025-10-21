using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    

    /// <summary>
    /// Reacción de un usuario a un contenido
    /// Un usuario solo puede tener UNA reacción por contenido
    /// </summary>
    public class Reaccion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [Required]
        public TipoReaccion TipoReaccion { get; set; }

        [Required]
        public DateTime FechaReaccion { get; set; }

        // Navegación
        [ForeignKey("ContenidoId")]
        public virtual Contenido Contenido { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }
    }
}