using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Tipos de transacciones de LadoCoins
    /// </summary>
    public enum TipoTransaccionLadoCoin
    {
        // ========================================
        // INGRESOS (Bonos y Premios)
        // ========================================

        /// <summary>Bono de bienvenida al registrarse ($20)</summary>
        BonoBienvenida = 0,

        /// <summary>Bono por primera publicación ($5)</summary>
        BonoPrimerContenido = 1,

        /// <summary>Bono por verificar email ($2)</summary>
        BonoVerificarEmail = 2,

        /// <summary>Bono por completar perfil ($3)</summary>
        BonoCompletarPerfil = 3,

        /// <summary>Bono por login diario ($0.50)</summary>
        BonoLoginDiario = 4,

        /// <summary>Bono por subir contenido diario ($1)</summary>
        BonoSubirContenido = 5,

        /// <summary>Bono por dar 5 likes al día ($0.25)</summary>
        BonoDarLikes = 6,

        /// <summary>Bono por 3 comentarios al día ($0.50)</summary>
        BonoComentar = 7,

        /// <summary>Bono por racha de 7 días ($5)</summary>
        BonoRacha7Dias = 8,

        /// <summary>Bono para el referidor cuando alguien se registra con su código ($10)</summary>
        BonoReferidor = 9,

        /// <summary>Bono para el usuario que se registra con código de referido ($15)</summary>
        BonoReferido = 10,

        /// <summary>Bono cuando un referido se convierte en creador LadoB ($50)</summary>
        BonoReferidoCreadorLadoB = 11,

        /// <summary>Comisión del 10% de premios del referido (por 3 meses)</summary>
        ComisionReferido = 12,

        /// <summary>LadoCoins recibidos como pago de otro usuario</summary>
        RecibirPago = 13,

        /// <summary>Ajuste manual por administrador</summary>
        AjusteAdmin = 14,

        // ========================================
        // GASTOS
        // ========================================

        /// <summary>Pago de suscripción (parcial o total en LC)</summary>
        PagoSuscripcion = 20,

        /// <summary>Pago de propina (hasta 100% en LC)</summary>
        PagoPropina = 21,

        /// <summary>Compra de crédito publicitario ($1 LC = $1.50 ads)</summary>
        CompraPublicidad = 22,

        /// <summary>Compra de boost de algoritmo ($1 LC = $2 boost)</summary>
        BoostAlgoritmo = 23,

        /// <summary>Compra de contenido individual</summary>
        CompraContenido = 24,

        // ========================================
        // DEDUCCIONES (No son gastos del usuario)
        // ========================================

        /// <summary>5% quemado en cada transacción de gasto</summary>
        Quema5Porciento = 30,

        /// <summary>LadoCoins vencidos (después de 30 días)</summary>
        Vencimiento = 31
    }

    /// <summary>
    /// Registro de movimientos de LadoCoins.
    /// Cada transacción tiene fecha de vencimiento para control de expiración.
    /// </summary>
    public class TransaccionLadoCoin
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [Display(Name = "Tipo de Transacción")]
        public TipoTransaccionLadoCoin Tipo { get; set; }

        /// <summary>
        /// Monto de la transacción. Positivo = ingreso, Negativo = gasto/deducción
        /// </summary>
        [Display(Name = "Monto")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        /// <summary>
        /// Monto quemado (5% de la transacción en gastos)
        /// </summary>
        [Display(Name = "Monto Quemado")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MontoQuemado { get; set; }

        /// <summary>
        /// Saldo antes de la transacción
        /// </summary>
        [Display(Name = "Saldo Anterior")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoAnterior { get; set; }

        /// <summary>
        /// Saldo después de la transacción
        /// </summary>
        [Display(Name = "Saldo Posterior")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoPosterior { get; set; }

        [Display(Name = "Descripción")]
        [StringLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// ID de referencia relacionado (ej: TransaccionId, ReferidoId, ContenidoId)
        /// </summary>
        [Display(Name = "Referencia")]
        [StringLength(100)]
        public string? ReferenciaId { get; set; }

        /// <summary>
        /// Tipo de referencia (ej: "Suscripcion", "Propina", "Referido")
        /// </summary>
        [Display(Name = "Tipo Referencia")]
        [StringLength(50)]
        public string? TipoReferencia { get; set; }

        [Display(Name = "Fecha de Transacción")]
        public DateTime FechaTransaccion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de vencimiento de los LadoCoins (FechaTransaccion + 30 días)
        /// Solo aplica a transacciones de ingreso
        /// </summary>
        [Display(Name = "Fecha de Vencimiento")]
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>
        /// Si los LadoCoins de esta transacción ya vencieron
        /// </summary>
        [Display(Name = "Vencido")]
        public bool Vencido { get; set; } = false;

        /// <summary>
        /// Monto restante de esta transacción (para tracking de vencimiento FIFO)
        /// </summary>
        [Display(Name = "Monto Restante")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoRestante { get; set; }

        // Navegación
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }
    }
}
