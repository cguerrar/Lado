using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Tabla de retenciones de impuestos por país.
    /// Estas tasas se aplican como valor predeterminado a los creadores según su país.
    /// </summary>
    public class RetencionPais
    {
        public int Id { get; set; }

        [Required]
        [StringLength(5)]
        [Display(Name = "Código de País")]
        public string CodigoPais { get; set; } = string.Empty; // MX, CO, AR, US, etc.

        [Required]
        [StringLength(100)]
        [Display(Name = "Nombre del País")]
        public string NombrePais { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Retención de Impuestos (%)")]
        [Range(0, 100)]
        public decimal PorcentajeRetencion { get; set; } = 0;

        [Display(Name = "Descripción/Notas")]
        [StringLength(500)]
        public string? Descripcion { get; set; } // Ej: "ISR sobre ingresos por servicios digitales"

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [Display(Name = "Última Actualización")]
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Lista de países con retenciones predeterminadas
    /// </summary>
    public static class RetencionesPredeterminadas
    {
        public static readonly Dictionary<string, (string Nombre, decimal Retencion, string Descripcion)> Paises = new()
        {
            { "MX", ("México", 10m, "ISR sobre servicios digitales") },
            { "CO", ("Colombia", 15m, "Retención en la fuente") },
            { "AR", ("Argentina", 35m, "Impuesto a las ganancias") },
            { "CL", ("Chile", 10m, "Impuesto adicional") },
            { "PE", ("Perú", 8m, "Impuesto a la renta") },
            { "BR", ("Brasil", 15m, "IRRF") },
            { "US", ("Estados Unidos", 30m, "Withholding tax (sin tratado)") },
            { "ES", ("España", 19m, "IRPF") },
            { "EC", ("Ecuador", 10m, "Impuesto a la renta") },
            { "VE", ("Venezuela", 34m, "ISLR") },
            { "UY", ("Uruguay", 12m, "IRPF") },
            { "PY", ("Paraguay", 10m, "Impuesto a la renta") },
            { "BO", ("Bolivia", 13m, "RC-IVA") },
            { "CR", ("Costa Rica", 15m, "Impuesto sobre la renta") },
            { "PA", ("Panamá", 0m, "Sin retención (territorial)") },
            { "DO", ("República Dominicana", 10m, "ISR") },
            { "GT", ("Guatemala", 5m, "ISR") },
            { "HN", ("Honduras", 10m, "ISR") },
            { "SV", ("El Salvador", 10m, "ISR") },
            { "NI", ("Nicaragua", 10m, "IR") }
        };
    }
}
