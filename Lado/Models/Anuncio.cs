using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Anuncio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AgenciaId { get; set; }

        [ForeignKey("AgenciaId")]
        public virtual Agencia? Agencia { get; set; }

        [Required]
        [StringLength(100)]
        public string Titulo { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [StringLength(500)]
        public string UrlDestino { get; set; } = string.Empty;

        [StringLength(500)]
        public string? UrlCreativo { get; set; }

        public TipoCreativo TipoCreativo { get; set; } = TipoCreativo.Imagen;

        public TextoBotonAnuncio TextoBoton { get; set; } = TextoBotonAnuncio.VerMas;

        [StringLength(50)]
        public string? TextoBotonPersonalizado { get; set; }

        // Presupuesto
        [Column(TypeName = "decimal(18,2)")]
        public decimal PresupuestoDiario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PresupuestoTotal { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoPorMilImpresiones { get; set; } // CPM

        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoPorClic { get; set; } // CPC

        // Metricas
        public long Impresiones { get; set; } = 0;
        public long Clics { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GastoTotal { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GastoHoy { get; set; } = 0;

        // Estado
        public EstadoAnuncio Estado { get; set; } = EstadoAnuncio.Borrador;

        // Fechas
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime? FechaAprobacion { get; set; }
        public DateTime? FechaPausa { get; set; }
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? MotivoRechazo { get; set; }

        // Navegacion
        public virtual SegmentacionAnuncio? Segmentacion { get; set; }
        public virtual ICollection<ImpresionAnuncio> ImpresionesDetalle { get; set; } = new List<ImpresionAnuncio>();
        public virtual ICollection<ClicAnuncio> ClicsDetalle { get; set; } = new List<ClicAnuncio>();

        // Propiedades calculadas
        [NotMapped]
        public decimal CTR => Impresiones > 0 ? Math.Round((decimal)Clics / Impresiones * 100, 2) : 0;

        [NotMapped]
        public decimal CostoPromedioPorClic => Clics > 0 ? Math.Round(GastoTotal / Clics, 4) : 0;

        [NotMapped]
        public string TextoBotonDisplay => TextoBoton switch
        {
            TextoBotonAnuncio.VerMas => "Ver mas",
            TextoBotonAnuncio.Suscribirse => "Suscribirse",
            TextoBotonAnuncio.Comprar => "Comprar",
            TextoBotonAnuncio.Descargar => "Descargar",
            TextoBotonAnuncio.Registrarse => "Registrarse",
            TextoBotonAnuncio.MasInformacion => "Mas informacion",
            _ => TextoBotonPersonalizado ?? "Ver mas"
        };
    }
}
