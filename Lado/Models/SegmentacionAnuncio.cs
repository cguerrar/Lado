using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class SegmentacionAnuncio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AnuncioId { get; set; }

        [ForeignKey("AnuncioId")]
        public virtual Anuncio? Anuncio { get; set; }

        // Segmentacion demografica
        public int? EdadMinima { get; set; }
        public int? EdadMaxima { get; set; }

        public GeneroUsuario? Genero { get; set; }

        // Ubicacion (JSON arrays)
        [StringLength(1000)]
        public string? PaisesJson { get; set; }  // ["ES", "MX", "AR"]

        [StringLength(2000)]
        public string? CiudadesJson { get; set; }  // ["Madrid", "Barcelona", "Ciudad de Mexico"]

        // Intereses (JSON array de IDs de CategoriaInteres)
        [StringLength(1000)]
        public string? InteresesJson { get; set; }  // [1, 2, 5, 8]

        // Dispositivos
        public bool? SoloMovil { get; set; }
        public bool? SoloDesktop { get; set; }

        // Horarios (JSON array de rangos)
        [StringLength(500)]
        public string? HorariosJson { get; set; }  // [{"inicio": "08:00", "fin": "22:00"}]

        // Dias de la semana (bitmask: 1=Lun, 2=Mar, 4=Mie, 8=Jue, 16=Vie, 32=Sab, 64=Dom)
        public int? DiasActivos { get; set; }  // 127 = todos los dias

        // Exclusiones
        [StringLength(1000)]
        public string? UsuariosExcluidosJson { get; set; }  // IDs de usuarios a excluir

        // Metadatos
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        // Helpers para deserializar
        [NotMapped]
        public List<string> Paises => string.IsNullOrEmpty(PaisesJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(PaisesJson) ?? new List<string>();

        [NotMapped]
        public List<string> Ciudades => string.IsNullOrEmpty(CiudadesJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(CiudadesJson) ?? new List<string>();

        [NotMapped]
        public List<int> Intereses => string.IsNullOrEmpty(InteresesJson)
            ? new List<int>()
            : System.Text.Json.JsonSerializer.Deserialize<List<int>>(InteresesJson) ?? new List<int>();

        // Helpers para verificar dias
        [NotMapped]
        public bool LunesActivo => ((DiasActivos ?? 127) & 1) == 1;
        [NotMapped]
        public bool MartesActivo => ((DiasActivos ?? 127) & 2) == 2;
        [NotMapped]
        public bool MiercolesActivo => ((DiasActivos ?? 127) & 4) == 4;
        [NotMapped]
        public bool JuevesActivo => ((DiasActivos ?? 127) & 8) == 8;
        [NotMapped]
        public bool ViernesActivo => ((DiasActivos ?? 127) & 16) == 16;
        [NotMapped]
        public bool SabadoActivo => ((DiasActivos ?? 127) & 32) == 32;
        [NotMapped]
        public bool DomingoActivo => ((DiasActivos ?? 127) & 64) == 64;
    }
}
