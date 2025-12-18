using System.ComponentModel.DataAnnotations;
using Lado.Models; // IMPORTANTE: Importar Models para usar TipoUsuario

namespace Lado.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Entre 3 y 30 caracteres")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Solo letras, números y guión bajo")]
        [Display(Name = "Nombre de usuario")]
        public string NombreUsuario { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Entre 2 y 100 caracteres")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*[0-9!@#$%^&*]).+$", ErrorMessage = "Debe contener letras y al menos un número o símbolo")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contraseña { get; set; }

        [Required(ErrorMessage = "Confirma tu contraseña")]
        [DataType(DataType.Password)]
        [Compare("Contraseña", ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContraseña { get; set; }

        [Display(Name = "Tipo de cuenta")]
        public TipoUsuario TipoUsuario { get; set; } = TipoUsuario.Creador;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los términos y condiciones")]
        [Display(Name = "Acepto los términos")]
        public bool AceptaTerminos { get; set; }

        [Required(ErrorMessage = "El seudónimo es obligatorio")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Entre 3 y 30 caracteres")]
        [RegularExpression(@"^[a-zA-Z0-9_\s]+$", ErrorMessage = "Solo letras, números, espacios y guión bajo")]
        [Display(Name = "Seudónimo")]
        public string Seudonimo { get; set; }

        [Display(Name = "Lado Preferido")]
        public TipoLado LadoPreferido { get; set; } = TipoLado.LadoA;

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

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email invalido")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Minimo 8 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma tu contraseña")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}