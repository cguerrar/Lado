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

        // Configuración Regional
        public const string ZONA_HORARIA = "ZonaHoraria";

        // ========================================
        // ALGORITMO "PARA TI" - Pesos (deben sumar 100)
        // ========================================
        public const string PARATI_PESO_ENGAGEMENT = "ParaTi_PesoEngagement";           // Default: 30
        public const string PARATI_PESO_INTERESES = "ParaTi_PesoIntereses";             // Default: 25
        public const string PARATI_PESO_CREADOR_FAVORITO = "ParaTi_PesoCreadorFavorito"; // Default: 20
        public const string PARATI_PESO_TIPO_CONTENIDO = "ParaTi_PesoTipoContenido";    // Default: 10
        public const string PARATI_PESO_RECENCIA = "ParaTi_PesoRecencia";               // Default: 15

        // ========================================
        // ALGORITMO "POR INTERESES" - Pesos (deben sumar 100)
        // ========================================
        public const string INTERESES_PESO_CATEGORIA = "Intereses_PesoCategoria";       // Default: 80
        public const string INTERESES_PESO_DESCUBRIMIENTO = "Intereses_PesoDescubrimiento"; // Default: 20

        // ========================================
        // CONFIGURACIÓN DE EMAIL
        // ========================================
        public const string EMAIL_PROVEEDOR_ACTIVO = "Email_ProveedorActivo";   // "Mailjet" o "AmazonSES"
        public const string EMAIL_FROM_EMAIL = "Email_FromEmail";               // Email del remitente
        public const string EMAIL_FROM_NAME = "Email_FromName";                 // Nombre del remitente

        // Mailjet
        public const string MAILJET_API_KEY = "Mailjet_ApiKey";
        public const string MAILJET_SECRET_KEY = "Mailjet_SecretKey";

        // Amazon SES
        public const string AMAZONSES_ACCESS_KEY = "AmazonSES_AccessKey";
        public const string AMAZONSES_SECRET_KEY = "AmazonSES_SecretKey";
        public const string AMAZONSES_REGION = "AmazonSES_Region";              // Default: us-east-1
    }
}
