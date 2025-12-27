using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Modelo para gestionar usuarios cuyas historias han sido silenciadas.
    /// Cuando un usuario silencia a otro, sus historias no aparecerán en el feed de stories.
    /// </summary>
    public class HistoriaSilenciada
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del usuario que silencia las historias
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        /// <summary>
        /// Usuario que silencia
        /// </summary>
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// ID del usuario cuyas historias son silenciadas
        /// </summary>
        [Required]
        public string SilenciadoId { get; set; } = string.Empty;

        /// <summary>
        /// Usuario silenciado
        /// </summary>
        [ForeignKey("SilenciadoId")]
        public virtual ApplicationUser? Silenciado { get; set; }

        /// <summary>
        /// Fecha en que se silenció
        /// </summary>
        public DateTime FechaSilenciado { get; set; } = DateTime.Now;
    }
}
