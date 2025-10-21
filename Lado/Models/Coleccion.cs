using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Colección o álbum de contenidos que se venden como paquete
    /// </summary>
    public class Coleccion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CreadorId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; }

        [MaxLength(1000)]
        public string? Descripcion { get; set; }

        [MaxLength(500)]
        public string? ImagenPortada { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioOriginal { get; set; } // Para mostrar descuento

        [Range(0, 100)]
        public int? DescuentoPorcentaje { get; set; }

        public bool EstaActiva { get; set; } = true;

        [Required]
        public DateTime FechaCreacion { get; set; }

        public DateTime? FechaActualizacion { get; set; }

        // Navegación
        [ForeignKey("CreadorId")]
        public virtual ApplicationUser Creador { get; set; }

        public virtual ICollection<ContenidoColeccion> Contenidos { get; set; }
        public virtual ICollection<CompraColeccion> Compras { get; set; }
    }

    /// <summary>
    /// Relación muchos a muchos entre Contenido y Colección
    /// </summary>
    public class ContenidoColeccion
    {
        [Required]
        public int ContenidoId { get; set; }

        [Required]
        public int ColeccionId { get; set; }

        [Required]
        public int Orden { get; set; } // Orden dentro de la colección

        // Navegación
        [ForeignKey("ContenidoId")]
        public virtual Contenido Contenido { get; set; }

        [ForeignKey("ColeccionId")]
        public virtual Coleccion Coleccion { get; set; }
    }

    /// <summary>
    /// Registro de compra de una colección completa
    /// </summary>
    public class CompraColeccion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ColeccionId { get; set; }

        [Required]
        public string CompradorId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Required]
        public DateTime FechaCompra { get; set; }

        // Navegación
        [ForeignKey("ColeccionId")]
        public virtual Coleccion Coleccion { get; set; }

        [ForeignKey("CompradorId")]
        public virtual ApplicationUser Comprador { get; set; }
    }
}