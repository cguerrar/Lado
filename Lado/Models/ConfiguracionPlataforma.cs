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
        public const string EMAIL_PROVEEDOR_ACTIVO = "Email_ProveedorActivo";   // "Mailjet", "AmazonSES" o "Brevo"
        public const string EMAIL_FROM_EMAIL = "Email_FromEmail";               // Email del remitente
        public const string EMAIL_FROM_NAME = "Email_FromName";                 // Nombre del remitente

        // Mailjet
        public const string MAILJET_API_KEY = "Mailjet_ApiKey";
        public const string MAILJET_SECRET_KEY = "Mailjet_SecretKey";

        // Amazon SES
        public const string AMAZONSES_ACCESS_KEY = "AmazonSES_AccessKey";
        public const string AMAZONSES_SECRET_KEY = "AmazonSES_SecretKey";
        public const string AMAZONSES_REGION = "AmazonSES_Region";              // Default: us-east-1

        // Brevo (antes Sendinblue)
        public const string BREVO_API_KEY = "Brevo_ApiKey";

        // ========================================
        // LÍMITES DE ARCHIVOS
        // ========================================
        public const string LIMITE_TAMANO_FOTO_MB = "Limite_TamanoFoto_MB";     // Default: 10
        public const string LIMITE_TAMANO_VIDEO_MB = "Limite_TamanoVideo_MB";   // Default: 100
        public const string LIMITE_CANTIDAD_ARCHIVOS = "Limite_CantidadArchivos"; // Default: 10

        // ========================================
        // DISTRIBUCIÓN DE PREVIEWS LADOB EN FEED
        // ========================================
        public const string LADOB_PREVIEW_CANTIDAD = "LadoB_Preview_Cantidad";   // Default: 1 (cuántos previews mostrar)
        public const string LADOB_PREVIEW_INTERVALO = "LadoB_Preview_Intervalo"; // Default: 5 (cada cuántos posts)

        // ========================================
        // LÍMITES DE CARGA DEL FEED
        // ========================================
        public const string FEED_LIMITE_LADOA = "Feed_Limite_LadoA";                     // Default: 30
        public const string FEED_LIMITE_LADOB_SUSCRIPTOS = "Feed_Limite_LadoB_Suscriptos"; // Default: 15
        public const string FEED_LIMITE_LADOB_PROPIO = "Feed_Limite_LadoB_Propio";       // Default: 10
        public const string FEED_LIMITE_COMPRADO = "Feed_Limite_Comprado";               // Default: 10
        public const string FEED_LIMITE_TOTAL = "Feed_Limite_Total";                     // Default: 50

        // ========================================
        // DESCUBRIMIENTO EN EL FEED
        // ========================================
        public const string FEED_DESCUBRIMIENTO_LADOA_CANTIDAD = "Feed_Descubrimiento_LadoA_Cantidad";   // Default: 5
        public const string FEED_DESCUBRIMIENTO_LADOB_CANTIDAD = "Feed_Descubrimiento_LadoB_Cantidad";   // Default: 5
        public const string FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD = "Feed_Descubrimiento_Usuarios";       // Default: 5

        // ========================================
        // VARIEDAD DE CREADORES EN FEED
        // ========================================
        public const string FEED_MAX_POSTS_CONSECUTIVOS_CREADOR = "Feed_MaxPostsConsecutivos"; // Default: 2

        // ========================================
        // INTERCALACIÓN DE ANUNCIOS EN FEED
        // ========================================
        public const string FEED_ANUNCIOS_INTERVALO = "Feed_Anuncios_Intervalo";   // Default: 8 (cada cuántos posts)
        public const string FEED_ANUNCIOS_CANTIDAD = "Feed_Anuncios_Cantidad";     // Default: 3 (cuántos anuncios cargar)

        // ========================================
        // EXPLORAR - LÍMITES Y CONFIGURACIÓN
        // ========================================
        public const string EXPLORAR_LIMITE_CREADORES = "Explorar_Limite_Creadores";           // Default: 50
        public const string EXPLORAR_LIMITE_CONTENIDO = "Explorar_Limite_Contenido";           // Default: 100
        public const string EXPLORAR_LIMITE_ZONAS_MAPA = "Explorar_Limite_ZonasMapa";          // Default: 20
        public const string EXPLORAR_LIMITE_CONTENIDO_MAPA = "Explorar_Limite_ContenidoMapa";  // Default: 30
        public const string EXPLORAR_PAGE_SIZE = "Explorar_PageSize";                          // Default: 30
        public const string EXPLORAR_CONFIANZA_OBJETOS = "Explorar_Confianza_Objetos";         // Default: 70 (0.7 * 100)
    }
}
