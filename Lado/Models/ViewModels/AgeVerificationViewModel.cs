using System.ComponentModel.DataAnnotations;

namespace Lado.Models.ViewModels
{
    /// <summary>
    /// ViewModel para el formulario de verificación de edad
    /// </summary>
    public class AgeVerificationViewModel
    {
        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria")]
        [Display(Name = "Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        public DateTime FechaNacimiento { get; set; }

        [Required(ErrorMessage = "El país es obligatorio")]
        [Display(Name = "País de Residencia")]
        [StringLength(5)]
        public string Pais { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debes aceptar los términos")]
        [Display(Name = "Acepto que tengo la edad mínima requerida")]
        public bool AceptoTerminos { get; set; }
    }
}