using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Lado.Models
{
    // ViewModel para la solicitud de verificación de creador
    // SIMPLIFICADO: Solo requiere selfie con documento para reducir fricción
    public class CreatorVerificationRequestViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [Display(Name = "Nombre Completo")]
        public string NombreCompleto { get; set; }

        [Required(ErrorMessage = "El tipo de documento es obligatorio")]
        [Display(Name = "Tipo de Documento")]
        public string TipoDocumento { get; set; } // DNI, Pasaporte, etc.

        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [Display(Name = "Número de Documento")]
        public string NumeroDocumento { get; set; }

        [Required(ErrorMessage = "El país es obligatorio")]
        [Display(Name = "País")]
        public string Pais { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "Formato de teléfono inválido")]
        [Display(Name = "Teléfono")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "Debes subir una selfie con tu documento")]
        [Display(Name = "Selfie con Documento")]
        public IFormFile SelfieConDocumento { get; set; }
    }

    // Modelo de base de datos para solicitudes de verificación de creadores
    // SIMPLIFICADO: Solo selfie con documento es obligatoria
    public class CreatorVerificationRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreCompleto { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoDocumento { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroDocumento { get; set; }

        [Required]
        [StringLength(5)]
        public string Pais { get; set; }

        // Ciudad y Dirección ya no son obligatorios
        [StringLength(100)]
        public string? Ciudad { get; set; }

        [StringLength(200)]
        public string? Direccion { get; set; }

        [Required]
        [StringLength(20)]
        public string Telefono { get; set; }

        // Documento de identidad por separado ya no es obligatorio
        public string? DocumentoIdentidadPath { get; set; }

        [Required]
        public string SelfieConDocumentoPath { get; set; }

        // Prueba de dirección eliminada (ya no se usa)
        public string? PruebaDireccionPath { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } // Pendiente, Aprobada, Rechazada

        [Required]
        public DateTime FechaSolicitud { get; set; }

        public DateTime? FechaRevision { get; set; }

        public string? RevisadoPor { get; set; }

        [StringLength(500)]
        public string? MotivoRechazo { get; set; }
    }
}
