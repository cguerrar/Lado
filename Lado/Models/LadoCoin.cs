using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Saldo de monedas premio (LadoCoins) por usuario.
    /// Los LadoCoins son una moneda virtual no canjeable por dinero real,
    /// pero sí utilizable para contenido LadoB, publicidad y boost de algoritmo.
    /// </summary>
    public class LadoCoin
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        /// <summary>
        /// Saldo disponible de LadoCoins activos
        /// </summary>
        [Display(Name = "Saldo Disponible")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoDisponible { get; set; } = 0;

        /// <summary>
        /// Saldo que vencerá en los próximos 7 días (para alertas)
        /// </summary>
        [Display(Name = "Saldo Por Vencer")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoPorVencer { get; set; } = 0;

        /// <summary>
        /// Total histórico de LadoCoins ganados
        /// </summary>
        [Display(Name = "Total Ganado")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGanado { get; set; } = 0;

        /// <summary>
        /// Total histórico de LadoCoins gastados
        /// </summary>
        [Display(Name = "Total Gastado")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGastado { get; set; } = 0;

        /// <summary>
        /// Total de LadoCoins quemados (5% por transacción + vencidos)
        /// </summary>
        [Display(Name = "Total Quemado")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalQuemado { get; set; } = 0;

        /// <summary>
        /// Total recibido por pagos de otros usuarios (creadores)
        /// </summary>
        [Display(Name = "Total Recibido")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRecibido { get; set; } = 0;

        [Display(Name = "Última Actualización")]
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        [Display(Name = "Fecha Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navegación
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }
    }
}
