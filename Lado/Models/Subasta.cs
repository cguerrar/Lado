using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Estado de una subasta
    /// </summary>
    public enum EstadoSubasta
    {
        Pendiente = 0,      // Programada para iniciar despues
        Activa = 1,         // En curso, recibiendo pujas
        Finalizada = 2,     // Termino con ganador
        Cancelada = 3,      // Cancelada por el creador
        SinOfertas = 4      // Termino sin pujas
    }

    /// <summary>
    /// Tipo de contenido en subasta
    /// </summary>
    public enum TipoContenidoSubasta
    {
        AccesoExclusivo = 0,        // Acceso exclusivo permanente
        PrimeroEnVer = 1,           // Primero en ver (luego se publica)
        ContenidoPersonalizado = 2, // Contenido hecho para el ganador
        ExperienciaVIP = 3          // Videollamada, mensaje especial, etc.
    }

    /// <summary>
    /// Representa una subasta de contenido exclusivo
    /// </summary>
    public class Subasta
    {
        public int Id { get; set; }

        // ========================================
        // CONTENIDO SUBASTADO (opcional)
        // ========================================

        /// <summary>
        /// ID del contenido que se subasta (si ya existe)
        /// </summary>
        public int? ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido? Contenido { get; set; }

        /// <summary>
        /// URL de preview/thumbnail para mostrar en la subasta
        /// </summary>
        [StringLength(500)]
        public string? ImagenPreview { get; set; }

        /// <summary>
        /// Tipo de contenido subastado
        /// </summary>
        [Display(Name = "Tipo de Contenido")]
        public TipoContenidoSubasta TipoContenidoSubasta { get; set; } = TipoContenidoSubasta.AccesoExclusivo;

        // ========================================
        // CREADOR
        // ========================================

        [Required]
        public string CreadorId { get; set; } = string.Empty;

        [ForeignKey("CreadorId")]
        public virtual ApplicationUser? Creador { get; set; }

        // ========================================
        // PRECIOS
        // ========================================

        [Required]
        [Display(Name = "Precio Inicial")]
        [Range(0.01, 999999.99)]
        public decimal PrecioInicial { get; set; }

        [Display(Name = "Precio Actual")]
        [Range(0, 999999.99)]
        public decimal PrecioActual { get; set; }

        [Display(Name = "Incremento Minimo")]
        [Range(0.01, 9999.99)]
        public decimal IncrementoMinimo { get; set; } = 1.00m;

        // ========================================
        // FECHAS
        // ========================================

        [Required]
        [Display(Name = "Fecha de Inicio")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Display(Name = "Fecha de Fin")]
        public DateTime FechaFin { get; set; }

        [Display(Name = "Fecha de Creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // ========================================
        // ESTADO
        // ========================================

        [Required]
        [Display(Name = "Estado")]
        public EstadoSubasta Estado { get; set; } = EstadoSubasta.Pendiente;

        /// <summary>
        /// Numero de pujas realizadas
        /// </summary>
        public int ContadorPujas { get; set; } = 0;

        /// <summary>
        /// Numero de visitas a la subasta
        /// </summary>
        public int NumeroVistas { get; set; } = 0;

        /// <summary>
        /// Si solo suscriptores pueden pujar
        /// </summary>
        [Display(Name = "Solo Suscriptores")]
        public bool SoloSuscriptores { get; set; } = false;

        /// <summary>
        /// Precio de compra inmediata (opcional) - "Compralo Ya"
        /// </summary>
        [Display(Name = "Precio Compralo Ya")]
        [Range(0.01, 999999.99)]
        public decimal? PrecioCompraloYa { get; set; }

        /// <summary>
        /// Si se muestra el historial de pujas publicamente
        /// </summary>
        public bool MostrarHistorialPujas { get; set; } = true;

        // ========================================
        // GANADOR
        // ========================================

        public string? GanadorId { get; set; }

        [ForeignKey("GanadorId")]
        public virtual ApplicationUser? Ganador { get; set; }

        // ========================================
        // CONFIGURACION ADICIONAL
        // ========================================

        [Display(Name = "Titulo")]
        [StringLength(200)]
        public string? Titulo { get; set; }

        [Display(Name = "Descripcion")]
        [StringLength(2000)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Si es true, extiende la subasta 5 minutos si hay puja en los ultimos 2 minutos
        /// </summary>
        [Display(Name = "Extension Automatica")]
        public bool ExtensionAutomatica { get; set; } = true;

        /// <summary>
        /// Numero de extensiones realizadas
        /// </summary>
        [Display(Name = "Extensiones Realizadas")]
        public int ExtensionesRealizadas { get; set; } = 0;

        /// <summary>
        /// Maximo de extensiones permitidas (0 = sin limite)
        /// </summary>
        [Display(Name = "Maximo Extensiones")]
        public int MaximoExtensiones { get; set; } = 5;

        // ========================================
        // RELACIONES
        // ========================================

        public virtual ICollection<SubastaPuja> Pujas { get; set; } = new List<SubastaPuja>();

        // ========================================
        // METODOS HELPER
        // ========================================

        /// <summary>
        /// Verifica si la subasta esta activa
        /// </summary>
        public bool EstaActiva()
        {
            return Estado == EstadoSubasta.Activa &&
                   DateTime.Now >= FechaInicio &&
                   DateTime.Now <= FechaFin;
        }

        /// <summary>
        /// Obtiene el tiempo restante de la subasta
        /// </summary>
        public TimeSpan? TiempoRestante()
        {
            if (!EstaActiva()) return null;
            return FechaFin - DateTime.Now;
        }

        /// <summary>
        /// Obtiene el numero total de pujas
        /// </summary>
        public int NumeroPujas => Pujas?.Count ?? 0;
    }

    /// <summary>
    /// Representa una puja en una subasta
    /// </summary>
    public class SubastaPuja
    {
        public int Id { get; set; }

        // ========================================
        // SUBASTA
        // ========================================

        [Required]
        public int SubastaId { get; set; }

        [ForeignKey("SubastaId")]
        public virtual Subasta? Subasta { get; set; }

        // ========================================
        // USUARIO QUE PUJA
        // ========================================

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        // ========================================
        // MONTO
        // ========================================

        [Required]
        [Display(Name = "Monto")]
        [Range(0.01, 999999.99)]
        public decimal Monto { get; set; }

        // ========================================
        // FECHA
        // ========================================

        [Required]
        [Display(Name = "Fecha de Puja")]
        public DateTime FechaPuja { get; set; } = DateTime.Now;

        // ========================================
        // ESTADO
        // ========================================

        /// <summary>
        /// Indica si esta puja fue superada por otra
        /// </summary>
        [Display(Name = "Es Superada")]
        public bool EsSuperada { get; set; } = false;

        /// <summary>
        /// Indica si es la puja ganadora
        /// </summary>
        [Display(Name = "Es Ganadora")]
        public bool EsGanadora { get; set; } = false;

        /// <summary>
        /// IP desde donde se realizo la puja (seguridad)
        /// </summary>
        [StringLength(50)]
        public string? IpAddress { get; set; }
    }
}
