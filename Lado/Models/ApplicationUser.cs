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

        // ========================================
        // SISTEMA DE IDENTIDAD DUAL (LADO A / LADO B)
        // ========================================

        // LADO B - Identidad Premium/Anónima
        [Display(Name = "Seudónimo")]
        [StringLength(50)]
        public string? Seudonimo { get; set; }

        [Display(Name = "Seudónimo Verificado")]
        public bool SeudonimoVerificado { get; set; } = false;

        [Display(Name = "Foto de Perfil LadoB")]
        public string? FotoPerfilLadoB { get; set; }

        [Display(Name = "Biografía LadoB")]
        [StringLength(500)]
        public string? BiografiaLadoB { get; set; }

        // === TIPO DE USUARIO ===
        [Display(Name = "Tipo de Usuario")]
        public int TipoUsuario { get; set; } = 0; // 0 = Fan, 1 = Creador, 2 = Agencia (Admin se maneja por Roles)

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

        // ========================================
        // MÉTODOS HELPER
        // ========================================

        /// <summary>
        /// Obtiene el nombre de visualización según el contexto y el tipo de lado.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la identidad de LadoB (seudónimo)</param>
        /// <returns>El nombre a mostrar</returns>
        public string ObtenerNombreDisplay(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(Seudonimo))
            {
                return Seudonimo;
            }
            return NombreCompleto;
        }

        /// <summary>
        /// Obtiene la foto de perfil según el contexto.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la foto de LadoB</param>
        /// <returns>La ruta de la foto o null</returns>
        public string? ObtenerFotoPerfil(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(FotoPerfilLadoB))
            {
                return FotoPerfilLadoB;
            }
            return FotoPerfil;
        }

        /// <summary>
        /// Obtiene la biografía según el contexto.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la biografía de LadoB</param>
        /// <returns>La biografía o null</returns>
        public string? ObtenerBiografia(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(BiografiaLadoB))
            {
                return BiografiaLadoB;
            }
            return Biografia;
        }

        /// <summary>
        /// Verifica si tiene configurada la identidad de LadoB.
        /// </summary>
        /// <returns>True si tiene seudónimo configurado</returns>
        public bool TieneLadoB()
        {
            return !string.IsNullOrEmpty(Seudonimo);
        }

        /// <summary>
        /// Valida si el usuario tiene la edad mínima requerida
        /// </summary>
        /// <param name="edadMinima">Edad mínima a validar (por defecto 18)</param>
        /// <returns>True si cumple con la edad mínima</returns>
        public bool TieneEdadMinima(int edadMinima = 18)
        {
            if (!FechaNacimiento.HasValue) return false;

            var edad = DateTime.Now.Year - FechaNacimiento.Value.Year;

            if (FechaNacimiento.Value.Date > DateTime.Now.AddYears(-edad))
                edad--;

            return edad >= edadMinima;
        }

        /// <summary>
        /// Calcula la edad actual del usuario
        /// </summary>
        /// <returns>Edad en años o null si no hay fecha de nacimiento</returns>
        public int? ObtenerEdad()
        {
            if (!FechaNacimiento.HasValue) return null;

            var edad = DateTime.Now.Year - FechaNacimiento.Value.Year;

            if (FechaNacimiento.Value.Date > DateTime.Now.AddYears(-edad))
                edad--;

            return edad;
        }

        /// <summary>
        /// Verifica si el usuario es un creador activo y verificado
        /// </summary>
        public bool EsCreadorActivo()
        {
            return TipoUsuario == 1 && EsCreador && EstaActivo;
        }

        /// <summary>
        /// Verifica si el usuario puede publicar contenido premium
        /// </summary>
        public bool PuedePublicarPremium()
        {
            return EsCreadorActivo() && CreadorVerificado;
        }

        /// <summary>
        /// Obtiene el nombre público (con seudónimo si está disponible)
        /// </summary>
        public string NombrePublico => !string.IsNullOrEmpty(Seudonimo) ? Seudonimo : NombreCompleto;

        /// <summary>
        /// Verifica si tiene verificación completa (edad + identidad si es creador)
        /// </summary>
        public bool TieneVerificacionCompleta()
        {
            if (!AgeVerified) return false;

            if (TipoUsuario == 1)
                return CreadorVerificado;

            return true;
        }

        /// <summary>
        /// Obtiene la inicial del nombre para mostrar en avatares.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la inicial del seudónimo</param>
        /// <returns>La primera letra del nombre/seudónimo</returns>
        public string ObtenerInicial(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(Seudonimo))
            {
                return Seudonimo.Substring(0, 1).ToUpper();
            }
            return NombreCompleto.Substring(0, 1).ToUpper();
        }
    }
}
