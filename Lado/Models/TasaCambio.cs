using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Tabla de tasas de cambio para conversión de monedas.
    /// La moneda base es USD.
    /// </summary>
    public class TasaCambio
    {
        public int Id { get; set; }

        [Required]
        [StringLength(3)]
        [Display(Name = "Código de Moneda")]
        public string CodigoMoneda { get; set; } = string.Empty; // USD, MXN, COP, EUR, etc.

        [Required]
        [StringLength(50)]
        [Display(Name = "Nombre de Moneda")]
        public string NombreMoneda { get; set; } = string.Empty; // Dólar, Peso Mexicano, etc.

        [Required]
        [StringLength(5)]
        [Display(Name = "Símbolo")]
        public string Simbolo { get; set; } = "$"; // $, €, etc.

        [Required]
        [Display(Name = "Tasa vs USD")]
        [Range(0.0001, 999999)]
        public decimal TasaVsUSD { get; set; } = 1; // 1 USD = X moneda local

        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        [Display(Name = "Última Actualización")]
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        // Método helper para convertir USD a esta moneda
        public decimal ConvertirDesdeUSD(decimal montoUSD)
        {
            return montoUSD * TasaVsUSD;
        }

        // Método helper para convertir esta moneda a USD
        public decimal ConvertirAUSD(decimal montoLocal)
        {
            if (TasaVsUSD == 0) return 0;
            return montoLocal / TasaVsUSD;
        }
    }

    /// <summary>
    /// Lista de monedas soportadas por defecto
    /// </summary>
    public static class MonedasSoportadas
    {
        public static readonly Dictionary<string, (string Nombre, string Simbolo, decimal TasaDefault)> Monedas = new()
        {
            { "USD", ("Dólar Estadounidense", "$", 1m) },
            { "MXN", ("Peso Mexicano", "$", 17.50m) },
            { "COP", ("Peso Colombiano", "$", 4000m) },
            { "ARS", ("Peso Argentino", "$", 875m) },
            { "EUR", ("Euro", "€", 0.92m) },
            { "BRL", ("Real Brasileño", "R$", 4.95m) },
            { "CLP", ("Peso Chileno", "$", 880m) },
            { "PEN", ("Sol Peruano", "S/", 3.75m) }
        };
    }
}
