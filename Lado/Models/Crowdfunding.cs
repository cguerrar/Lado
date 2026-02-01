using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Estado de una campana de crowdfunding
    /// </summary>
    public enum EstadoCampanaCrowdfunding
    {
        Borrador = 0,       // Aun no publicada
        Activa = 1,         // Recibiendo aportes
        MetaAlcanzada = 2,  // Meta lograda, esperando finalizacion
        Exitosa = 3,        // Campana exitosa, contenido entregado
        Fallida = 4,        // No alcanzo la meta, aportes devueltos
        Cancelada = 5       // Cancelada por el creador
    }

    /// <summary>
    /// Estado de un aporte individual
    /// </summary>
    public enum EstadoAporte
    {
        Pendiente = 0,      // Aporte registrado
        Confirmado = 1,     // Pago confirmado
        Devuelto = 2,       // Dinero devuelto (campana fallida)
        Reembolsado = 3     // Reembolsado por solicitud
    }

    /// <summary>
    /// Campana de crowdfunding de contenido
    /// </summary>
    public class CampanaCrowdfunding
    {
        public int Id { get; set; }

        [Required]
        public string CreadorId { get; set; } = string.Empty;

        [ForeignKey("CreadorId")]
        public virtual ApplicationUser? Creador { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Titulo")]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        [Display(Name = "Descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>
        /// Monto meta a alcanzar
        /// </summary>
        [Required]
        [Range(10, 100000)]
        [Display(Name = "Meta")]
        public decimal Meta { get; set; }

        /// <summary>
        /// Monto minimo de aporte permitido
        /// </summary>
        [Range(1, 1000)]
        [Display(Name = "Aporte Minimo")]
        public decimal AporteMinimo { get; set; } = 1m;

        /// <summary>
        /// Total recaudado hasta el momento
        /// </summary>
        [Display(Name = "Total Recaudado")]
        public decimal TotalRecaudado { get; set; } = 0m;

        /// <summary>
        /// Numero total de aportantes
        /// </summary>
        [Display(Name = "Total Aportantes")]
        public int TotalAportantes { get; set; } = 0;

        /// <summary>
        /// Imagen de preview/promocion de la campana
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Imagen Preview")]
        public string? ImagenPreview { get; set; }

        /// <summary>
        /// Video de preview (opcional)
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Video Preview")]
        public string? VideoPreview { get; set; }

        /// <summary>
        /// Estado actual de la campana
        /// </summary>
        [Display(Name = "Estado")]
        public EstadoCampanaCrowdfunding Estado { get; set; } = EstadoCampanaCrowdfunding.Borrador;

        /// <summary>
        /// Fecha de creacion
        /// </summary>
        [Display(Name = "Fecha Creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de publicacion
        /// </summary>
        [Display(Name = "Fecha Publicacion")]
        public DateTime? FechaPublicacion { get; set; }

        /// <summary>
        /// Fecha limite para alcanzar la meta
        /// </summary>
        [Required]
        [Display(Name = "Fecha Limite")]
        public DateTime FechaLimite { get; set; }

        /// <summary>
        /// Fecha de finalizacion (cuando se proceso como exitosa o fallida)
        /// </summary>
        [Display(Name = "Fecha Finalizacion")]
        public DateTime? FechaFinalizacion { get; set; }

        /// <summary>
        /// Tipo de lado al que pertenece el contenido prometido
        /// </summary>
        [Display(Name = "Tipo Lado")]
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        /// <summary>
        /// Categoria del contenido prometido
        /// </summary>
        [StringLength(50)]
        [Display(Name = "Categoria")]
        public string? Categoria { get; set; }

        /// <summary>
        /// Tags separados por coma
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Tags")]
        public string? Tags { get; set; }

        /// <summary>
        /// ID del contenido entregado (cuando se completa la campana)
        /// </summary>
        public int? ContenidoEntregadoId { get; set; }

        [ForeignKey("ContenidoEntregadoId")]
        public virtual Contenido? ContenidoEntregado { get; set; }

        /// <summary>
        /// Mensaje de agradecimiento a los aportantes
        /// </summary>
        [StringLength(1000)]
        [Display(Name = "Mensaje Agradecimiento")]
        public string? MensajeAgradecimiento { get; set; }

        /// <summary>
        /// Si es true, la campana es visible en el listado publico
        /// </summary>
        [Display(Name = "Es Visible")]
        public bool EsVisible { get; set; } = true;

        /// <summary>
        /// Vistas de la campana
        /// </summary>
        [Display(Name = "Vistas")]
        public int Vistas { get; set; } = 0;

        // Navegacion
        public virtual ICollection<AporteCrowdfunding> Aportes { get; set; } = new List<AporteCrowdfunding>();

        // ========================================
        // METODOS HELPER
        // ========================================

        /// <summary>
        /// Calcula el porcentaje de progreso hacia la meta
        /// </summary>
        public decimal PorcentajeProgreso => Meta > 0 ? Math.Min(100, (TotalRecaudado / Meta) * 100) : 0;

        /// <summary>
        /// Verifica si se alcanzo la meta
        /// </summary>
        public bool MetaAlcanzada => TotalRecaudado >= Meta;

        /// <summary>
        /// Verifica si la campana esta expirada
        /// </summary>
        public bool EstaExpirada => DateTime.Now > FechaLimite;

        /// <summary>
        /// Dias restantes para la fecha limite
        /// </summary>
        public int DiasRestantes => Math.Max(0, (FechaLimite.Date - DateTime.Now.Date).Days);

        /// <summary>
        /// Horas restantes si quedan menos de 24 horas
        /// </summary>
        public int HorasRestantes => Math.Max(0, (int)(FechaLimite - DateTime.Now).TotalHours);

        /// <summary>
        /// Obtiene el texto descriptivo del tiempo restante
        /// </summary>
        public string TiempoRestanteTexto
        {
            get
            {
                if (EstaExpirada) return "Finalizada";
                if (DiasRestantes > 0) return $"{DiasRestantes} dias";
                if (HorasRestantes > 0) return $"{HorasRestantes} horas";
                var minutos = Math.Max(0, (int)(FechaLimite - DateTime.Now).TotalMinutes);
                return $"{minutos} minutos";
            }
        }

        /// <summary>
        /// Verifica si se puede aportar a esta campana
        /// </summary>
        public bool PuedeRecibAportes => Estado == EstadoCampanaCrowdfunding.Activa && !EstaExpirada;

        /// <summary>
        /// Monto faltante para alcanzar la meta
        /// </summary>
        public decimal MontoFaltante => Math.Max(0, Meta - TotalRecaudado);
    }

    /// <summary>
    /// Aporte individual a una campana de crowdfunding
    /// </summary>
    public class AporteCrowdfunding
    {
        public int Id { get; set; }

        [Required]
        public int CampanaId { get; set; }

        [ForeignKey("CampanaId")]
        public virtual CampanaCrowdfunding? Campana { get; set; }

        [Required]
        public string AportanteId { get; set; } = string.Empty;

        [ForeignKey("AportanteId")]
        public virtual ApplicationUser? Aportante { get; set; }

        /// <summary>
        /// Monto aportado
        /// </summary>
        [Required]
        [Range(1, 100000)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        /// <summary>
        /// Estado del aporte
        /// </summary>
        [Display(Name = "Estado")]
        public EstadoAporte Estado { get; set; } = EstadoAporte.Confirmado;

        /// <summary>
        /// Fecha del aporte
        /// </summary>
        [Display(Name = "Fecha Aporte")]
        public DateTime FechaAporte { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de devolucion (si aplica)
        /// </summary>
        [Display(Name = "Fecha Devolucion")]
        public DateTime? FechaDevolucion { get; set; }

        /// <summary>
        /// Mensaje opcional del aportante
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Mensaje")]
        public string? Mensaje { get; set; }

        /// <summary>
        /// Si el aportante desea aparecer anonimo
        /// </summary>
        [Display(Name = "Es Anonimo")]
        public bool EsAnonimo { get; set; } = false;

        /// <summary>
        /// Si ya recibio acceso al contenido entregado
        /// </summary>
        [Display(Name = "Acceso Otorgado")]
        public bool AccesoOtorgado { get; set; } = false;

        /// <summary>
        /// Fecha en que se otorgo el acceso
        /// </summary>
        [Display(Name = "Fecha Acceso")]
        public DateTime? FechaAcceso { get; set; }

        /// <summary>
        /// Referencia a la transaccion (para auditoría)
        /// </summary>
        public int? TransaccionId { get; set; }

        /// <summary>
        /// Referencia a la transaccion de devolucion (si aplica)
        /// </summary>
        public int? TransaccionDevolucionId { get; set; }
    }
}
