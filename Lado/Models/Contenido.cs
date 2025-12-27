using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Contenido
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [Required]
        public TipoContenido TipoContenido { get; set; }

        public string? Descripcion { get; set; }
        public string? RutaArchivo { get; set; }
        public string? Thumbnail { get; set; }

        // ========================================
        // CATEGORIZACION PARA TARGETING
        // ========================================
        public int? CategoriaInteresId { get; set; }

        [ForeignKey("CategoriaInteresId")]
        public virtual CategoriaInteres? CategoriaInteres { get; set; }

        [StringLength(500)]
        public string? Tags { get; set; }  // JSON array: ["fitness", "lifestyle", "travel"]

        // ========================================
        // SISTEMA DE MONETIZACIÓN
        // ========================================

        public bool EsPremium { get; set; } = false;

        public decimal? PrecioDesbloqueo { get; set; }

        // ========================================
        // SISTEMA LADO A / LADO B
        // ========================================

        [Required]
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        public bool EsGratis { get; set; } = true;

        /// <summary>
        /// Indica si el contenido es un Reel (video corto vertical desde el creador de Reels)
        /// vs un Post con video normal
        /// </summary>
        public bool EsReel { get; set; } = false;

        // ========================================
        // VISIBILIDAD PÚBLICA PARA FEED GENERAL
        // ========================================

        /// <summary>
        /// Si es true, este contenido se mostrará en el feed público para usuarios anónimos.
        /// Solo aplica para contenido LadoA (el contenido LadoB siempre requiere suscripción).
        /// </summary>
        public bool EsPublicoGeneral { get; set; } = false;

        public string? NombreMostrado { get; set; }

        // ========================================
        // SISTEMA DE PREVIEWS
        // ========================================

        public bool TienePreview { get; set; } = false;

        public int? DuracionPreviewSegundos { get; set; }

        public string? RutaPreview { get; set; }

        // ========================================
        // PREVIEW BLUR DE LADOB EN LADOA
        // ========================================

        /// <summary>
        /// Si es true, este contenido es un preview censurado de un contenido LadoB
        /// </summary>
        public bool EsPreviewBlurDeLadoB { get; set; } = false;

        /// <summary>
        /// ID del contenido LadoB original (si este es un preview blur)
        /// </summary>
        public int? ContenidoOriginalLadoBId { get; set; }

        /// <summary>
        /// Tipo de censura aplicada al preview
        /// </summary>
        public TipoCensuraPreview? TipoCensuraPreview { get; set; }

        // ========================================
        // MÚSICA ASOCIADA (para fotos con audio)
        // ========================================

        /// <summary>
        /// ID de la pista musical asociada (para fotos con música estilo Reels/TikTok)
        /// </summary>
        public int? PistaMusicalId { get; set; }

        [ForeignKey("PistaMusicalId")]
        public virtual PistaMusical? PistaMusical { get; set; }

        /// <summary>
        /// Volumen de la música (0.0 a 1.0)
        /// </summary>
        public decimal? MusicaVolumen { get; set; }

        /// <summary>
        /// Volumen del audio original del video (0.0 a 1.0)
        /// </summary>
        public decimal? AudioOriginalVolumen { get; set; }

        /// <summary>
        /// Segundo de inicio del recorte de audio
        /// </summary>
        public int? AudioTrimInicio { get; set; }

        /// <summary>
        /// Duración del audio en segundos (desde AudioTrimInicio)
        /// </summary>
        public int? AudioDuracion { get; set; }

        // ========================================
        // ESTADO DEL CONTENIDO
        // ========================================

        public bool EsBorrador { get; set; } = false;
        public bool EstaActivo { get; set; } = true;
        public bool Censurado { get; set; } = false;
        public string? RazonCensura { get; set; }

        /// <summary>
        /// Si es true, el contenido se muestra con advertencia de contenido sensible
        /// (el usuario debe hacer clic para ver)
        /// </summary>
        public bool EsContenidoSensible { get; set; } = false;

        /// <summary>
        /// Si es true, el contenido solo es visible para el creador (privado)
        /// </summary>
        public bool EsPrivado { get; set; } = false;

        /// <summary>
        /// Si es true, los comentarios están desactivados para este contenido
        /// </summary>
        public bool ComentariosDesactivados { get; set; } = false;

        // ========================================
        // UBICACIÓN GEOGRÁFICA (desde EXIF)
        // ========================================

        /// <summary>
        /// Latitud extraída de los metadatos EXIF de la imagen
        /// </summary>
        public double? Latitud { get; set; }

        /// <summary>
        /// Longitud extraída de los metadatos EXIF de la imagen
        /// </summary>
        public double? Longitud { get; set; }

        /// <summary>
        /// Nombre de la ubicación (ej: "Santiago, Chile")
        /// </summary>
        [StringLength(200)]
        public string? NombreUbicacion { get; set; }

        // ========================================
        // MÉTRICAS
        // ========================================

        public int NumeroLikes { get; set; } = 0;
        public int NumeroComentarios { get; set; } = 0;
        public int NumeroVistas { get; set; } = 0;
        public int NumeroCompartidos { get; set; } = 0;

        // ========================================
        // FECHAS
        // ========================================

        public DateTime FechaPublicacion { get; set; } = DateTime.Now;
        public DateTime? FechaActualizacion { get; set; }

        // ========================================
        // RELACIONES
        // ========================================

        public ApplicationUser? Usuario { get; set; }

        public ICollection<Like> Likes { get; set; } = new List<Like>();

        public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

        // Nuevas relaciones para sistema premium
        public ICollection<Reaccion> Reacciones { get; set; } = new List<Reaccion>();

        public ICollection<ContenidoColeccion> Colecciones { get; set; } = new List<ContenidoColeccion>();

        public ICollection<CompraContenido> Compras { get; set; } = new List<CompraContenido>();

        // ========================================
        // OBJETOS DETECTADOS (para busqueda visual)
        // ========================================

        /// <summary>
        /// Objetos detectados automaticamente en el contenido visual.
        /// Permite busquedas tipo "mostrar contenido con motos".
        /// </summary>
        public ICollection<ObjetoContenido> ObjetosDetectados { get; set; } = new List<ObjetoContenido>();

        // ========================================
        // MÚLTIPLES ARCHIVOS (CARRUSEL)
        // ========================================

        /// <summary>
        /// Colección de archivos del post (carrusel).
        /// Si está vacío, usar RutaArchivo para compatibilidad.
        /// </summary>
        public ICollection<ArchivoContenido> Archivos { get; set; } = new List<ArchivoContenido>();

        /// <summary>
        /// Indica si este contenido tiene múltiples archivos (es carrusel)
        /// </summary>
        [NotMapped]
        public bool EsCarrusel => Archivos?.Count > 1;

        /// <summary>
        /// Obtiene todos los archivos del contenido (compatibilidad con posts antiguos)
        /// </summary>
        [NotMapped]
        public List<ArchivoContenido> TodosLosArchivos
        {
            get
            {
                // Si tiene archivos en la colección, usarlos
                if (Archivos?.Any() == true)
                {
                    return Archivos.OrderBy(a => a.Orden).ToList();
                }

                // Compatibilidad con posts antiguos que solo tienen RutaArchivo
                if (!string.IsNullOrEmpty(RutaArchivo))
                {
                    return new List<ArchivoContenido>
                    {
                        new ArchivoContenido
                        {
                            Id = 0,
                            ContenidoId = Id,
                            RutaArchivo = RutaArchivo,
                            Orden = 0,
                            TipoArchivo = TipoContenido == TipoContenido.Video ? TipoArchivo.Video : TipoArchivo.Foto,
                            Thumbnail = Thumbnail
                        }
                    };
                }

                return new List<ArchivoContenido>();
            }
        }

        /// <summary>
        /// Obtiene la URL del primer archivo (para previews y thumbnails)
        /// </summary>
        [NotMapped]
        public string? PrimerArchivo => TodosLosArchivos.FirstOrDefault()?.RutaArchivo ?? RutaArchivo;

        /// <summary>
        /// Número total de archivos en el post
        /// </summary>
        [NotMapped]
        public int NumeroArchivos => TodosLosArchivos.Count;
    }
}