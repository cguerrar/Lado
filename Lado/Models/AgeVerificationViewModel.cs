using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    // ViewModel para el formulario de verificación de edad
    public class AgeVerificationViewModel
    {
        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria")]
        [Display(Name = "Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        public DateTime FechaNacimiento { get; set; }

        [Required(ErrorMessage = "El país es obligatorio")]
        [Display(Name = "País de Residencia")]
        public string Pais { get; set; }

        [Required(ErrorMessage = "Debes aceptar los términos")]
        [Display(Name = "Acepto que tengo la edad mínima requerida")]
        public bool AceptoTerminos { get; set; }
    }

    // Modelo para el log de verificaciones de edad
    public class AgeVerificationLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public DateTime FechaVerificacion { get; set; }
        public string Pais { get; set; }
        public int EdadAlVerificar { get; set; }
        public string? IpAddress { get; set; }

        // Relación con el usuario
        public virtual ApplicationUser User { get; set; }
    }
}