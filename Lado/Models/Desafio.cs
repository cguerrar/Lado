using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [StringLength(2000)]
        public string Descripcion { get; set; }

        // ========================================
        // PRESUPUESTO FLEXIBLE (Fase 1)
        // ========================================
        [Required]
        [Range(5, 10000)]
        public decimal PresupuestoMinimo { get; set; } = 10.00m;

        [Range(5, 10000)]
        public decimal? PresupuestoMaximo { get; set; }

        // Tipo de presupuesto: Fijo o Rango
        public TipoPresupuesto TipoPresupuesto { get; set; } = TipoPresupuesto.Fijo;

        // Mantener compatibilidad con código existente
        [NotMapped]
        public decimal Presupuesto
        {
            get => PresupuestoMinimo;
            set => PresupuestoMinimo = value;
        }

        // Precio final acordado
        public decimal? PrecioFinal { get; set; }

        [Required]
        [Range(1, 30)]
        public int DiasPlazoPlazo { get; set; } = 7;

        [Required]
        [StringLength(50)]
        public string Categoria { get; set; }

        // Tags adicionales para mejor búsqueda
        [StringLength(500)]
        public string? Tags { get; set; }

        [Required]
        public TipoContenidoDesafio TipoContenido { get; set; }

        // Tipo de contenido requerido (permite "Cualquiera")
        public TipoContenidoDesafio TipoContenidoRequerido { get; set; } = TipoContenidoDesafio.Cualquiera;

        [Required]
        public VisibilidadDesafio Visibilidad { get; set; }

        [Required]
        public EstadoDesafio Estado { get; set; } = EstadoDesafio.Pendiente;

        // ========================================
        // TIPOS ESPECIALES DE DESAFÍO (Fase 3)
        // ========================================
        public TipoDesafioEspecial TipoEspecial { get; set; } = TipoDesafioEspecial.Normal;

        // Para desafíos relámpago (24h)
        public bool EsRelampago { get; set; } = false;

        // Límite de propuestas (0 = sin límite)
        public int LimitePropuestas { get; set; } = 0;

        // Prioridad/Boost (pagado para aparecer primero)
        public bool EsDestacado { get; set; } = false;
        public DateTime? FechaFinDestacado { get; set; }

        // ========================================
        // FECHAS
        // ========================================
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaExpiracion { get; set; }

        public DateTime? FechaAsignacion { get; set; }

        public DateTime? FechaCompletado { get; set; }

        public DateTime? FechaEntrega { get; set; }

        // ========================================
        // PAGOS
        // ========================================
        public string? StripePaymentIntentId { get; set; }

        [Required]
        public EstadoPago EstadoPago { get; set; } = EstadoPago.Hold;

        // Monto en escrow
        public decimal? MontoEscrow { get; set; }

        // ========================================
        // CONTENIDO Y ENTREGA
        // ========================================
        public string? RutaContenido { get; set; }
        public string? ArchivoEntregaUrl { get; set; }
        public string? NotasCreador { get; set; }

        // ========================================
        // RATING Y REPUTACIÓN (Fase 2)
        // ========================================
        public int? RatingFan { get; set; } // Rating que da el fan al creador
        public int? RatingCreador { get; set; } // Rating que da el creador al fan
        public string? ComentarioRatingFan { get; set; }
        public string? ComentarioRatingCreador { get; set; }

        // Mantener compatibilidad
        [NotMapped]
        public int? Rating
        {
            get => RatingFan;
            set => RatingFan = value;
        }

        [NotMapped]
        public string? ComentarioRating
        {
            get => ComentarioRatingFan;
            set => ComentarioRatingFan = value;
        }

        // ========================================
        // ESTADÍSTICAS
        // ========================================
        public int NumeroVistas { get; set; } = 0;
        public int NumeroGuardados { get; set; } = 0; // Fans que lo guardaron para después

        // ========================================
        // RELACIONES
        // ========================================
        public virtual ICollection<PropuestaDesafio> Propuestas { get; set; } = new List<PropuestaDesafio>();
        public virtual ICollection<MensajeDesafio> Mensajes { get; set; } = new List<MensajeDesafio>();
        public virtual ICollection<DesafioGuardado> Guardados { get; set; } = new List<DesafioGuardado>();
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
        Cualquiera = -1,
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
        Liberado = 4,
        EnEscrow = 5
    }

    public enum TipoPresupuesto
    {
        Fijo = 0,           // Precio fijo
        Rango = 1,          // Rango min-max
        MejorOferta = 2     // El creador propone
    }

    public enum TipoDesafioEspecial
    {
        Normal = 0,
        Relampago = 1,      // 24 horas
        Premium = 2,        // Mayor pago, mejor servicio
        Grupal = 3,         // Varios creadores
        Recurrente = 4      // Semanal/mensual
    }

    // ========================================
    // MODELO: MENSAJES DEL DESAFÍO (Chat)
    // ========================================
    public class MensajeDesafio
    {
        public int Id { get; set; }

        [Required]
        public int DesafioId { get; set; }
        public virtual Desafio Desafio { get; set; }

        [Required]
        public string EmisorId { get; set; }
        public virtual ApplicationUser Emisor { get; set; }

        [Required]
        [StringLength(2000)]
        public string Contenido { get; set; }

        public string? ArchivoUrl { get; set; }

        public TipoMensajeDesafio Tipo { get; set; } = TipoMensajeDesafio.Texto;

        [Required]
        public DateTime FechaEnvio { get; set; } = DateTime.Now;

        public bool Leido { get; set; } = false;
        public DateTime? FechaLectura { get; set; }
    }

    public enum TipoMensajeDesafio
    {
        Texto = 0,
        Imagen = 1,
        Archivo = 2,
        Sistema = 3     // Mensajes automáticos del sistema
    }

    // ========================================
    // MODELO: DESAFÍOS GUARDADOS
    // ========================================
    public class DesafioGuardado
    {
        public int Id { get; set; }

        [Required]
        public int DesafioId { get; set; }
        public virtual Desafio Desafio { get; set; }

        [Required]
        public string UsuarioId { get; set; }
        public virtual ApplicationUser Usuario { get; set; }

        [Required]
        public DateTime FechaGuardado { get; set; } = DateTime.Now;
    }

    // ========================================
    // MODELO: BADGES/LOGROS DE USUARIO
    // ========================================
    public class BadgeUsuario
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }
        public virtual ApplicationUser Usuario { get; set; }

        [Required]
        public TipoBadge TipoBadge { get; set; }

        [Required]
        public DateTime FechaObtenido { get; set; } = DateTime.Now;

        // Datos adicionales del badge
        public string? DatosExtra { get; set; }
    }

    public enum TipoBadge
    {
        // Badges de Creador
        PrimerDesafioCompletado = 0,
        CincoDesafiosCompletados = 1,
        VeinteDesafiosCompletados = 2,
        CienDesafiosCompletados = 3,
        RatingPerfecto = 4,         // 5 estrellas en 10+ desafíos
        EntregaRapida = 5,          // Entrega antes del plazo
        TopCreadorMes = 6,
        TopCreadorSemana = 7,
        CreadorVerificado = 8,
        CreadorPremium = 9,

        // Badges de Fan
        PrimerDesafioCreado = 20,
        FanFrecuente = 21,          // 10+ desafíos creados
        FanGeneroso = 22,           // Pagos mayores al promedio
        BuenPagador = 23,           // Siempre paga a tiempo
        FanVerificado = 24,

        // Badges especiales
        MiembroFundador = 50,
        BetaTester = 51,
        Influencer = 52
    }

    // ========================================
    // MODELO: ESTADÍSTICAS DE DESAFÍOS DEL USUARIO
    // ========================================
    public class EstadisticasDesafiosUsuario
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }
        public virtual ApplicationUser Usuario { get; set; }

        // Como Creador
        public int DesafiosCompletadosComoCreador { get; set; } = 0;
        public int DesafiosEnProgresoComoCreador { get; set; } = 0;
        public decimal TotalGanadoDesafios { get; set; } = 0;
        public double PromedioRatingComoCreador { get; set; } = 0;
        public int TotalRatingsComoCreador { get; set; } = 0;
        public double TasaCompletado { get; set; } = 0; // % de desafíos completados vs asignados
        public double TiempoPromedioEntrega { get; set; } = 0; // En días

        // Como Fan
        public int DesafiosCreadosComoFan { get; set; } = 0;
        public int DesafiosCompletadosComoFan { get; set; } = 0;
        public decimal TotalGastadoDesafios { get; set; } = 0;
        public double PromedioRatingComoFan { get; set; } = 0;
        public int TotalRatingsComoFan { get; set; } = 0;

        // Nivel y puntos
        public int NivelCreador { get; set; } = 1;
        public int PuntosCreador { get; set; } = 0;
        public int NivelFan { get; set; } = 1;
        public int PuntosFan { get; set; } = 0;

        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        // Propiedades de conveniencia (combinan creador y fan)
        [NotMapped]
        public int Nivel => Math.Max(NivelCreador, NivelFan);

        [NotMapped]
        public int PuntosExperiencia
        {
            get => PuntosCreador + PuntosFan;
            set
            {
                PuntosCreador = value / 2;
                PuntosFan = value - PuntosCreador;
            }
        }

        [NotMapped]
        public double PromedioRating => TotalRatingsComoCreador + TotalRatingsComoFan > 0
            ? (PromedioRatingComoCreador * TotalRatingsComoCreador + PromedioRatingComoFan * TotalRatingsComoFan)
              / (TotalRatingsComoCreador + TotalRatingsComoFan)
            : 0;

        [NotMapped]
        public int DesafiosCompletados => DesafiosCompletadosComoCreador + DesafiosCompletadosComoFan;

        [NotMapped]
        public int DesafiosCreados
        {
            get => DesafiosCreadosComoFan;
            set => DesafiosCreadosComoFan = value;
        }

        [NotMapped]
        public decimal TotalGastado
        {
            get => TotalGastadoDesafios;
            set => TotalGastadoDesafios = value;
        }
    }

    // ========================================
    // MODELO: NOTIFICACIÓN DE DESAFÍO
    // ========================================
    public class NotificacionDesafio
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }
        public virtual ApplicationUser Usuario { get; set; }

        public int? DesafioId { get; set; }
        public virtual Desafio? Desafio { get; set; }

        [Required]
        public TipoNotificacionDesafio Tipo { get; set; }

        [Required]
        [StringLength(500)]
        public string Mensaje { get; set; }

        public string? Url { get; set; }

        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public bool Leida { get; set; } = false;
        public DateTime? FechaLectura { get; set; }
    }

    public enum TipoNotificacionDesafio
    {
        NuevaPropuesta = 0,
        PropuestaAceptada = 1,
        PropuestaRechazada = 2,
        ContenidoEntregado = 3,
        ContenidoAprobado = 4,
        CorreccionSolicitada = 5,
        NuevoMensaje = 6,
        DesafioPorExpirar = 7,
        DesafioExpirado = 8,
        PagoLiberado = 9,
        NuevoBadge = 10,
        SubioDeNivel = 11
    }
}