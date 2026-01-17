using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Anuncio
    {
        [Key]
        public int Id { get; set; }

        // AgenciaId es nullable para permitir anuncios de Lado (empresa)
        public int? AgenciaId { get; set; }

        [ForeignKey("AgenciaId")]
        public virtual Agencia? Agencia { get; set; }

        // Indica si es un anuncio interno de Lado (no de agencia)
        public bool EsAnuncioLado { get; set; } = false;

        // Prioridad del anuncio (mayor = más probabilidad de mostrarse)
        // Los anuncios de Lado pueden tener prioridad alta
        public int Prioridad { get; set; } = 1;

        // Mostrar en Stories para TODOS los usuarios (sin segmentación)
        // Solo un anuncio puede tener esto activo a la vez
        public bool MostrarEnStoriesGlobal { get; set; } = false;

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

        // ========================================
        // UBICACIONES DEL ANUNCIO
        // ========================================

        /// <summary>
        /// Mostrar en el Feed principal (entre publicaciones)
        /// </summary>
        public bool MostrarEnFeed { get; set; } = true;

        /// <summary>
        /// Cada cuántas publicaciones aparece en el Feed (ej: 5 = cada 5 posts)
        /// </summary>
        public int FrecuenciaEnFeed { get; set; } = 5;

        /// <summary>
        /// Mostrar en Stories (como historia patrocinada)
        /// </summary>
        public bool MostrarEnStories { get; set; } = false;

        /// <summary>
        /// Mostrar en la página Explorar
        /// </summary>
        public bool MostrarEnExplorar { get; set; } = false;

        /// <summary>
        /// Mostrar banner en la parte superior
        /// </summary>
        public bool MostrarBannerSuperior { get; set; } = false;

        /// <summary>
        /// Mostrar banner en la parte inferior
        /// </summary>
        public bool MostrarBannerInferior { get; set; } = false;

        /// <summary>
        /// Mostrar en perfiles de creadores
        /// </summary>
        public bool MostrarEnPerfiles { get; set; } = false;

        // ========================================
        // CONTROL DE FRECUENCIA POR USUARIO
        // ========================================

        /// <summary>
        /// Máximo de veces que un usuario puede ver este anuncio (0 = ilimitado)
        /// </summary>
        public int MaxImpresionesUsuario { get; set; } = 0;

        /// <summary>
        /// Máximo de impresiones por usuario por día (0 = ilimitado)
        /// </summary>
        public int MaxImpresionesUsuarioDia { get; set; } = 3;

        /// <summary>
        /// Minutos mínimos entre impresiones al mismo usuario (0 = sin límite)
        /// </summary>
        public int MinutosEntreImpresiones { get; set; } = 30;

        // ========================================
        // CARRUSEL DE IMÁGENES
        // ========================================

        /// <summary>
        /// Si es carrusel, URLs de imágenes adicionales (JSON array)
        /// </summary>
        [StringLength(4000)]
        public string? ImagenesCarruselJson { get; set; }

        /// <summary>
        /// Indica si es un anuncio tipo carrusel
        /// </summary>
        public bool EsCarrusel { get; set; } = false;

        // ========================================
        // SEGMENTACIÓN POR TIPO DE USUARIO
        // ========================================

        /// <summary>
        /// Mostrar solo a creadores
        /// </summary>
        public bool SoloCreadores { get; set; } = false;

        /// <summary>
        /// Mostrar solo a fans (no creadores)
        /// </summary>
        public bool SoloFans { get; set; } = false;

        /// <summary>
        /// Mostrar solo a usuarios verificados
        /// </summary>
        public bool SoloVerificados { get; set; } = false;

        /// <summary>
        /// Mostrar solo a usuarios con suscripciones activas
        /// </summary>
        public bool SoloConSuscripciones { get; set; } = false;

        // ========================================
        // PRESUPUESTO
        // ========================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal PresupuestoDiario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PresupuestoTotal { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoPorMilImpresiones { get; set; } // CPM

        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoPorClic { get; set; } // CPC

        // ========================================
        // MÉTRICAS
        // ========================================

        public long Impresiones { get; set; } = 0;
        public long Clics { get; set; } = 0;
        public long ImpresionesHoy { get; set; } = 0;
        public long ClicsHoy { get; set; } = 0;
        public long UsuariosUnicos { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GastoTotal { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GastoHoy { get; set; } = 0;

        /// <summary>
        /// Fecha del último reset de métricas diarias
        /// </summary>
        public DateTime? FechaUltimoResetDiario { get; set; }

        // ========================================
        // ESTADO Y FECHAS
        // ========================================

        public EstadoAnuncio Estado { get; set; } = EstadoAnuncio.Borrador;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime? FechaAprobacion { get; set; }
        public DateTime? FechaPausa { get; set; }
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? MotivoRechazo { get; set; }

        /// <summary>
        /// Nombre interno para identificar el anuncio (útil para A/B testing)
        /// </summary>
        [StringLength(100)]
        public string? NombreInterno { get; set; }

        /// <summary>
        /// Notas internas del administrador
        /// </summary>
        [StringLength(1000)]
        public string? NotasInternas { get; set; }

        // ========================================
        // NAVEGACIÓN
        // ========================================

        public virtual SegmentacionAnuncio? Segmentacion { get; set; }
        public virtual ICollection<ImpresionAnuncio> ImpresionesDetalle { get; set; } = new List<ImpresionAnuncio>();
        public virtual ICollection<ClicAnuncio> ClicsDetalle { get; set; } = new List<ClicAnuncio>();
        public virtual ICollection<VistaAnuncioUsuario> VistasUsuarios { get; set; } = new List<VistaAnuncioUsuario>();

        // ========================================
        // PROPIEDADES CALCULADAS
        // ========================================

        [NotMapped]
        public decimal CTR => Impresiones > 0 ? Math.Round((decimal)Clics / Impresiones * 100, 2) : 0;

        [NotMapped]
        public decimal CostoPromedioPorClic => Clics > 0 ? Math.Round(GastoTotal / Clics, 4) : 0;

        [NotMapped]
        public List<string> ImagenesCarrusel => string.IsNullOrEmpty(ImagenesCarruselJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImagenesCarruselJson) ?? new List<string>();

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

        [NotMapped]
        public string UbicacionesResumen
        {
            get
            {
                var ubicaciones = new List<string>();
                if (MostrarEnFeed) ubicaciones.Add("Feed");
                if (MostrarEnStories || MostrarEnStoriesGlobal) ubicaciones.Add("Stories");
                if (MostrarEnExplorar) ubicaciones.Add("Explorar");
                if (MostrarBannerSuperior) ubicaciones.Add("Banner Superior");
                if (MostrarBannerInferior) ubicaciones.Add("Banner Inferior");
                if (MostrarEnPerfiles) ubicaciones.Add("Perfiles");
                return ubicaciones.Any() ? string.Join(", ", ubicaciones) : "Ninguna";
            }
        }
    }

    /// <summary>
    /// Registro de vistas de anuncios por usuario para control de frecuencia
    /// </summary>
    public class VistaAnuncioUsuario
    {
        [Key]
        public long Id { get; set; }

        public int AnuncioId { get; set; }

        [ForeignKey("AnuncioId")]
        public virtual Anuncio Anuncio { get; set; } = null!;

        [Required]
        [StringLength(450)]
        public string UsuarioId { get; set; } = null!;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; } = null!;

        /// <summary>
        /// Total de impresiones para este usuario
        /// </summary>
        public int TotalImpresiones { get; set; } = 0;

        /// <summary>
        /// Impresiones de hoy
        /// </summary>
        public int ImpresionesHoy { get; set; } = 0;

        /// <summary>
        /// Fecha de la primera impresión
        /// </summary>
        public DateTime PrimeraImpresion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de la última impresión
        /// </summary>
        public DateTime UltimaImpresion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha del último reset diario
        /// </summary>
        public DateTime? FechaUltimoReset { get; set; }

        /// <summary>
        /// Si el usuario hizo clic
        /// </summary>
        public bool HizoClic { get; set; } = false;

        /// <summary>
        /// Total de clics del usuario
        /// </summary>
        public int TotalClics { get; set; } = 0;
    }
}
