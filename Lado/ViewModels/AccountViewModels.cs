using System.ComponentModel.DataAnnotations;
using Lado.Models; // IMPORTANTE: Importar Models para usar TipoUsuario

namespace Lado.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres")]
        [Display(Name = "Nombre de usuario")]
        public string NombreUsuario { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100)]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contraseña { get; set; }

        [Required(ErrorMessage = "Confirma tu contraseña")]
        [DataType(DataType.Password)]
        [Compare("Contraseña", ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContraseña { get; set; }

        [Required]
        [Display(Name = "Tipo de cuenta")]
        public TipoUsuario TipoUsuario { get; set; }

        [Required(ErrorMessage = "Debes aceptar los términos")]
        public bool AceptaTerminos { get; set; }

        [Required(ErrorMessage = "El seudónimo es obligatorio")]
        [StringLength(50)]
        [Display(Name = "Seudónimo")]
        public string Seudonimo { get; set; }

    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email o usuario requerido")]
        [Display(Name = "Email o nombre de usuario")]
        public string EmailOUsuario { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contraseña { get; set; }

        [Display(Name = "Recordarme")]
        public bool Recordarme { get; set; }
    }
}