using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models;

/// <summary>
/// Representa una historia guardada en destacados (no expira)
/// Similar a los "Highlights" de Instagram
/// </summary>
public class HistoriaDestacada
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Usuario dueño de los destacados
    /// </summary>
    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey("UsuarioId")]
    public virtual ApplicationUser? Usuario { get; set; }

    /// <summary>
    /// Grupo/Álbum de destacados (ej: "Viajes", "Trabajo", etc.)
    /// </summary>
    public int? GrupoDestacadoId { get; set; }

    [ForeignKey("GrupoDestacadoId")]
    public virtual GrupoDestacado? GrupoDestacado { get; set; }

    /// <summary>
    /// Ruta del archivo multimedia (copiado de la story original)
    /// </summary>
    [Required]
    [StringLength(500)]
    public string RutaArchivo { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de contenido (VIDEO, FOTO, etc.)
    /// </summary>
    public TipoContenido TipoContenido { get; set; }

    /// <summary>
    /// Elementos visuales del editor (JSON)
    /// </summary>
    public string? ElementosJson { get; set; }

    /// <summary>
    /// ID de la story original (puede ser null si la story expiró)
    /// </summary>
    public int? StoryOriginalId { get; set; }

    /// <summary>
    /// Fecha en que se agregó a destacados
    /// </summary>
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    /// <summary>
    /// Orden dentro del grupo
    /// </summary>
    public int Orden { get; set; }

    /// <summary>
    /// Número de vistas del destacado
    /// </summary>
    public int NumeroVistas { get; set; }

    /// <summary>
    /// Tipo de lado (A o B)
    /// </summary>
    public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

    /// <summary>
    /// Música asociada (opcional)
    /// </summary>
    public int? PistaMusicalId { get; set; }

    [ForeignKey("PistaMusicalId")]
    public virtual PistaMusical? PistaMusical { get; set; }

    public int MusicaInicioSegundos { get; set; }
    public int MusicaVolumen { get; set; } = 50;
}

/// <summary>
/// Grupo/Álbum de historias destacadas
/// Permite organizar destacados por categorías
/// </summary>
public class GrupoDestacado
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey("UsuarioId")]
    public virtual ApplicationUser? Usuario { get; set; }

    /// <summary>
    /// Nombre del grupo (ej: "Viajes", "Comida", "Trabajo")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Imagen de portada del grupo (primera historia o personalizada)
    /// </summary>
    [StringLength(500)]
    public string? ImagenPortada { get; set; }

    /// <summary>
    /// Orden en el perfil
    /// </summary>
    public int Orden { get; set; }

    /// <summary>
    /// Fecha de creación
    /// </summary>
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    /// <summary>
    /// Tipo de lado
    /// </summary>
    public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

    /// <summary>
    /// Historias en este grupo
    /// </summary>
    public virtual ICollection<HistoriaDestacada> Historias { get; set; } = new List<HistoriaDestacada>();
}

/// <summary>
/// Registro de envío de story a usuario específico
/// </summary>
public class StoryEnviada
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Story que se envía
    /// </summary>
    [Required]
    public int StoryId { get; set; }

    [ForeignKey("StoryId")]
    public virtual Story? Story { get; set; }

    /// <summary>
    /// Usuario que envía
    /// </summary>
    [Required]
    public string RemitenteId { get; set; } = string.Empty;

    [ForeignKey("RemitenteId")]
    public virtual ApplicationUser? Remitente { get; set; }

    /// <summary>
    /// Usuario que recibe
    /// </summary>
    [Required]
    public string DestinatarioId { get; set; } = string.Empty;

    [ForeignKey("DestinatarioId")]
    public virtual ApplicationUser? Destinatario { get; set; }

    /// <summary>
    /// Fecha de envío
    /// </summary>
    public DateTime FechaEnvio { get; set; } = DateTime.Now;

    /// <summary>
    /// Si el destinatario ya vio la story enviada
    /// </summary>
    public bool Visto { get; set; }

    /// <summary>
    /// Fecha en que se vio
    /// </summary>
    public DateTime? FechaVisto { get; set; }

    /// <summary>
    /// Mensaje opcional al enviar
    /// </summary>
    [StringLength(500)]
    public string? Mensaje { get; set; }
}
