using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Modelo para popups administrables desde el panel de admin
    /// </summary>
    public class Popup
    {
        // ====================================
        // IDENTIFICACION
        // ====================================

        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre interno para identificar el popup en admin
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        // ====================================
        // TIPO Y CONTENIDO
        // ====================================

        [Required]
        public TipoPopup Tipo { get; set; } = TipoPopup.Modal;

        [StringLength(200)]
        public string? Titulo { get; set; }

        /// <summary>
        /// Contenido HTML del popup
        /// </summary>
        public string? Contenido { get; set; }

        /// <summary>
        /// URL de imagen principal (puede ser ruta local o URL externa)
        /// </summary>
        [StringLength(500)]
        public string? ImagenUrl { get; set; }

        /// <summary>
        /// Clase de icono Font Awesome (ej: "fas fa-bell")
        /// </summary>
        [StringLength(50)]
        public string? IconoClase { get; set; }

        /// <summary>
        /// JSON array de botones: [{texto, url, estilo, accion}]
        /// </summary>
        public string? BotonesJson { get; set; }

        // ====================================
        // DISENO
        // ====================================

        [Required]
        public PosicionPopup Posicion { get; set; } = PosicionPopup.Centro;

        /// <summary>
        /// Color de fondo (hex o rgba)
        /// </summary>
        [StringLength(50)]
        public string? ColorFondo { get; set; }

        /// <summary>
        /// Color del texto principal
        /// </summary>
        [StringLength(50)]
        public string? ColorTexto { get; set; }

        /// <summary>
        /// Color del boton primario
        /// </summary>
        [StringLength(50)]
        public string? ColorBotonPrimario { get; set; }

        /// <summary>
        /// CSS personalizado adicional
        /// </summary>
        public string? CssPersonalizado { get; set; }

        [Required]
        public AnimacionPopup Animacion { get; set; } = AnimacionPopup.FadeIn;

        /// <summary>
        /// Ancho maximo del popup en pixeles
        /// </summary>
        public int AnchoMaximo { get; set; } = 400;

        /// <summary>
        /// Mostrar boton de cerrar (X)
        /// </summary>
        public bool MostrarBotonCerrar { get; set; } = true;

        /// <summary>
        /// Cerrar al hacer click fuera del popup
        /// </summary>
        public bool CerrarAlClickFuera { get; set; } = true;

        // ====================================
        // TRIGGERS
        // ====================================

        [Required]
        public TriggerPopup Trigger { get; set; } = TriggerPopup.Inmediato;

        /// <summary>
        /// Segundos de delay (si Trigger = Delay)
        /// </summary>
        public int? DelaySegundos { get; set; }

        /// <summary>
        /// Porcentaje de scroll (si Trigger = Scroll)
        /// </summary>
        public int? ScrollPorcentaje { get; set; }

        /// <summary>
        /// Numero de visitas requeridas (si Trigger = Visitas)
        /// </summary>
        public int? VisitasRequeridas { get; set; }

        /// <summary>
        /// Selector CSS del elemento (si Trigger = Click)
        /// </summary>
        [StringLength(200)]
        public string? SelectorClick { get; set; }

        // ====================================
        // SEGMENTACION
        // ====================================

        /// <summary>
        /// Mostrar a usuarios logueados
        /// </summary>
        public bool MostrarUsuariosLogueados { get; set; } = true;

        /// <summary>
        /// Mostrar a usuarios anonimos (no logueados)
        /// </summary>
        public bool MostrarUsuariosAnonimos { get; set; } = true;

        /// <summary>
        /// Mostrar en dispositivos moviles
        /// </summary>
        public bool MostrarEnMovil { get; set; } = true;

        /// <summary>
        /// Mostrar en desktop
        /// </summary>
        public bool MostrarEnDesktop { get; set; } = true;

        /// <summary>
        /// Paginas donde mostrar (separadas por coma). * = todas
        /// Ej: /Feed,/Feed/Perfil,/Contenido
        /// </summary>
        [StringLength(1000)]
        public string? PaginasIncluidas { get; set; }

        /// <summary>
        /// Paginas donde NO mostrar (separadas por coma)
        /// </summary>
        [StringLength(1000)]
        public string? PaginasExcluidas { get; set; }

        // ====================================
        // PWA (Progressive Web App)
        // ====================================

        /// <summary>
        /// Habilita funcionalidad PWA (detecta instalabilidad, iOS, standalone)
        /// </summary>
        public bool EsPWA { get; set; } = false;

        /// <summary>
        /// Solo mostrar si la app se puede instalar (no esta en modo standalone)
        /// </summary>
        public bool SoloSiInstalable { get; set; } = true;

        /// <summary>
        /// Contenido HTML alternativo para usuarios iOS (instrucciones manuales)
        /// </summary>
        public string? ContenidoIOS { get; set; }

        /// <summary>
        /// Texto del boton de instalar (default: "Instalar")
        /// </summary>
        [StringLength(50)]
        public string? TextoBotonInstalar { get; set; }

        /// <summary>
        /// Mostrar solo en iOS
        /// </summary>
        public bool SoloIOS { get; set; } = false;

        /// <summary>
        /// Mostrar solo en Android/Chrome (donde beforeinstallprompt funciona)
        /// </summary>
        public bool SoloAndroid { get; set; } = false;

        // ====================================
        // FRECUENCIA
        // ====================================

        [Required]
        public FrecuenciaPopup Frecuencia { get; set; } = FrecuenciaPopup.UnaVez;

        /// <summary>
        /// Dias entre mostrar (si Frecuencia = CadaXDias)
        /// </summary>
        public int? DiasFrecuencia { get; set; }

        // ====================================
        // ESTADO Y PROGRAMACION
        // ====================================

        /// <summary>
        /// Si el popup esta activo
        /// </summary>
        public bool EstaActivo { get; set; } = true;

        /// <summary>
        /// Fecha de inicio (null = sin limite)
        /// </summary>
        public DateTime? FechaInicio { get; set; }

        /// <summary>
        /// Fecha de fin (null = sin limite)
        /// </summary>
        public DateTime? FechaFin { get; set; }

        /// <summary>
        /// Prioridad 1-10 (mayor = mas prioritario)
        /// </summary>
        public int Prioridad { get; set; } = 5;

        // ====================================
        // METRICAS
        // ====================================

        /// <summary>
        /// Veces que se ha mostrado
        /// </summary>
        public int Impresiones { get; set; } = 0;

        /// <summary>
        /// Clics en botones del popup
        /// </summary>
        public int Clics { get; set; } = 0;

        /// <summary>
        /// Veces que se ha cerrado manualmente
        /// </summary>
        public int Cierres { get; set; } = 0;

        // ====================================
        // AUDITORIA
        // ====================================

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? UltimaModificacion { get; set; }

        // ====================================
        // PROPIEDADES CALCULADAS
        // ====================================

        /// <summary>
        /// Click-through rate (CTR)
        /// </summary>
        [NotMapped]
        public decimal CTR => Impresiones > 0
            ? Math.Round((decimal)Clics / Impresiones * 100, 2)
            : 0;

        /// <summary>
        /// Tasa de cierre
        /// </summary>
        [NotMapped]
        public decimal TasaCierre => Impresiones > 0
            ? Math.Round((decimal)Cierres / Impresiones * 100, 2)
            : 0;

        /// <summary>
        /// Si el popup debe mostrarse segun fechas
        /// </summary>
        [NotMapped]
        public bool EstaEnRangoFechas
        {
            get
            {
                var ahora = DateTime.Now;
                if (FechaInicio.HasValue && ahora < FechaInicio.Value) return false;
                if (FechaFin.HasValue && ahora > FechaFin.Value) return false;
                return true;
            }
        }

        /// <summary>
        /// Nombre del tipo para mostrar en UI
        /// </summary>
        [NotMapped]
        public string TipoDisplay => Tipo switch
        {
            TipoPopup.Banner => "Banner",
            TipoPopup.Modal => "Modal",
            TipoPopup.Toast => "Toast",
            TipoPopup.FullScreen => "Pantalla completa",
            TipoPopup.Slide => "Panel lateral",
            _ => "Desconocido"
        };

        /// <summary>
        /// Nombre de la posicion para mostrar en UI
        /// </summary>
        [NotMapped]
        public string PosicionDisplay => Posicion switch
        {
            PosicionPopup.Centro => "Centro",
            PosicionPopup.TopLeft => "Arriba izquierda",
            PosicionPopup.TopCenter => "Arriba centro",
            PosicionPopup.TopRight => "Arriba derecha",
            PosicionPopup.BottomLeft => "Abajo izquierda",
            PosicionPopup.BottomCenter => "Abajo centro",
            PosicionPopup.BottomRight => "Abajo derecha",
            PosicionPopup.Left => "Izquierda",
            PosicionPopup.Right => "Derecha",
            _ => "Centro"
        };
    }
}
