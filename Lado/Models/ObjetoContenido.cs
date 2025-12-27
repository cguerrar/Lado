using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Representa un objeto detectado automaticamente en el contenido visual.
    /// Usado para busquedas y agrupacion por elementos visuales (ej: motos, perros, playa).
    /// </summary>
    public class ObjetoContenido
    {
        public int Id { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        /// <summary>
        /// Nombre del objeto detectado en espanol, minusculas, sin tildes, singular.
        /// Ejemplos: "moto", "perro", "playa", "persona", "carro"
        /// </summary>
        [Required]
        [StringLength(50)]
        public string NombreObjeto { get; set; } = string.Empty;

        /// <summary>
        /// Nivel de confianza de la deteccion (0.0 a 1.0).
        /// Solo se guardan objetos con confianza >= 0.7
        /// </summary>
        [Range(0.0, 1.0)]
        public float Confianza { get; set; }

        /// <summary>
        /// Fecha en que se detecto el objeto
        /// </summary>
        public DateTime FechaDeteccion { get; set; } = DateTime.Now;

        [ForeignKey("ContenidoId")]
        public virtual Contenido? Contenido { get; set; }
    }
}
