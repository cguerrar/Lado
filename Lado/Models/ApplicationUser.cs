using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    public class ApplicationUser : IdentityUser
    {
        // === INFORMACIÓN BÁSICA ===
        [Display(Name = "Nombre Completo")]
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Display(Name = "Biografía")]
        [StringLength(500)]
        public string? Biografia { get; set; }

        [Display(Name = "Foto de Perfil")]
        public string? FotoPerfil { get; set; }

        [Display(Name = "Foto de Portada")]
        public string? FotoPortada { get; set; }

        // === TIPO DE USUARIO ===
        [Display(Name = "Tipo de Usuario")]
        public int TipoUsuario { get; set; } // 0 = Fan, 1 = Creador, 2 = Admin

        [Display(Name = "Es Creador")]
        public bool EsCreador { get; set; } = false;

        // === INFORMACIÓN DE CREADOR ===
        [Display(Name = "Precio de Suscripción")]
        [Range(0, 999999)]
        public decimal PrecioSuscripcion { get; set; } = 9.99m;

        [Display(Name = "Categoría")]
        [StringLength(50)]
        public string? Categoria { get; set; }

        [Display(Name = "Número de Seguidores")]
        public int NumeroSeguidores { get; set; } = 0;

        // === FINANZAS ===
        [Display(Name = "Saldo Disponible")]
        public decimal Saldo { get; set; } = 0;

        [Display(Name = "Total de Ganancias")]
        public decimal TotalGanancias { get; set; } = 0;

        // === ESTADO DE CUENTA ===
        [Display(Name = "Cuenta Activa")]
        public bool EstaActivo { get; set; } = true;

        [Display(Name = "Usuario Verificado")]
        public bool EsVerificado { get; set; } = false;

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // === RELACIONES ===
        public ICollection<Suscripcion> Suscripciones { get; set; } = new List<Suscripcion>();
        public ICollection<Suscripcion> Suscriptores { get; set; } = new List<Suscripcion>();

        // ========================================
        // VERIFICACIÓN DE EDAD
        // ========================================
        [Display(Name = "Fecha de Nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Display(Name = "País de Residencia")]
        [StringLength(5)]
        public string? Pais { get; set; }

        [Display(Name = "Edad Verificada")]
        public bool AgeVerified { get; set; } = false;

        [Display(Name = "Fecha de Verificación de Edad")]
        public DateTime? AgeVerifiedDate { get; set; }

        // ========================================
        // VERIFICACIÓN DE IDENTIDAD (CREADORES)
        // ========================================
        [Display(Name = "Creador Verificado")]
        public bool CreadorVerificado { get; set; } = false;

        [Display(Name = "Fecha de Verificación de Identidad")]
        public DateTime? FechaVerificacion { get; set; }
    }
}