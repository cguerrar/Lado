using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Configuración general de la plataforma, editable desde Admin
    /// </summary>
    public class ConfiguracionPlataforma
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Clave única de la configuración
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Clave { get; set; } = string.Empty;

        /// <summary>
        /// Valor de la configuración
        /// </summary>
        [StringLength(500)]
        public string Valor { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del campo
        /// </summary>
        [StringLength(255)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Categoría para agrupar configuraciones
        /// </summary>
        [StringLength(50)]
        public string Categoria { get; set; } = "General";

        /// <summary>
        /// Última modificación
        /// </summary>
        public DateTime UltimaModificacion { get; set; } = DateTime.Now;

        // ========================================
        // CLAVES CONOCIDAS (constantes)
        // ========================================

        public const string COMISION_BILLETERA_ELECTRONICA = "ComisionBilleteraElectronica";
        public const string TIEMPO_PROCESO_RETIRO = "TiempoProcesoRetiro";
        public const string MONTO_MINIMO_RECARGA = "MontoMinimoRecarga";
        public const string MONTO_MAXIMO_RECARGA = "MontoMaximoRecarga";
        public const string COMISION_PLATAFORMA = "ComisionPlataforma";
    }
}
