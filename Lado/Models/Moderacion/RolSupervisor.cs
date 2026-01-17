using System.ComponentModel.DataAnnotations;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Define un rol de supervisor con un conjunto de permisos
    /// </summary>
    public class RolSupervisor
    {
        public int Id { get; set; }

        /// <summary>Nombre del rol (ej: "Supervisor de Contenido")</summary>
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Descripción del rol y sus responsabilidades</summary>
        [MaxLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>Color del badge para identificar el rol en UI</summary>
        [MaxLength(20)]
        public string ColorBadge { get; set; } = "#4682B4";

        /// <summary>Icono FontAwesome para el rol</summary>
        [MaxLength(50)]
        public string Icono { get; set; } = "fa-user-shield";

        /// <summary>Si el rol está activo</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Fecha de creación</summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>Máximo de items que puede tener asignados simultáneamente</summary>
        public int MaxItemsSimultaneos { get; set; } = 5;

        // Navegación
        public virtual ICollection<RolSupervisorPermiso> RolesPermisos { get; set; } = new List<RolSupervisorPermiso>();
        public virtual ICollection<UsuarioSupervisor> Usuarios { get; set; } = new List<UsuarioSupervisor>();
    }

    /// <summary>
    /// Tabla de unión entre RolSupervisor y PermisoSupervisor (muchos a muchos)
    /// </summary>
    public class RolSupervisorPermiso
    {
        public int Id { get; set; }

        public int RolSupervisorId { get; set; }
        public virtual RolSupervisor RolSupervisor { get; set; } = null!;

        public int PermisoSupervisorId { get; set; }
        public virtual PermisoSupervisor PermisoSupervisor { get; set; } = null!;
    }

    /// <summary>
    /// Roles predefinidos del sistema
    /// </summary>
    public static class RolesPredefinidos
    {
        public const string SUPERVISOR_CONTENIDO = "SupervisorContenido";
        public const string SUPERVISOR_REPORTES = "SupervisorReportes";
        public const string SUPERVISOR_VERIFICACION = "SupervisorVerificacion";
        public const string SUPERVISOR_SENIOR = "SupervisorSenior"; // Todos los permisos de supervisor

        public static List<(string Nombre, string Descripcion, string Color, string Icono, string[] Permisos)> ObtenerRolesPredefinidos()
        {
            return new List<(string, string, string, string, string[])>
            {
                (
                    "Supervisor de Contenido",
                    "Revisa y modera el contenido subido por los creadores",
                    "#10b981", // Verde
                    "fa-images",
                    new[] {
                        PermisosPredefinidos.CONTENIDO_VER_COLA,
                        PermisosPredefinidos.CONTENIDO_APROBAR,
                        PermisosPredefinidos.CONTENIDO_RECHAZAR,
                        PermisosPredefinidos.CONTENIDO_CENSURAR,
                        PermisosPredefinidos.CONTENIDO_ESCALAR,
                        PermisosPredefinidos.CONTENIDO_VER_HISTORIAL,
                        PermisosPredefinidos.ESTADISTICAS_PROPIAS
                    }
                ),
                (
                    "Supervisor de Reportes",
                    "Gestiona los reportes de usuarios y contenido",
                    "#f59e0b", // Amarillo
                    "fa-flag",
                    new[] {
                        PermisosPredefinidos.REPORTES_VER,
                        PermisosPredefinidos.REPORTES_RESOLVER,
                        PermisosPredefinidos.REPORTES_ESCALAR,
                        PermisosPredefinidos.USUARIOS_VER_BASICO,
                        PermisosPredefinidos.USUARIOS_ADVERTIR,
                        PermisosPredefinidos.ESTADISTICAS_PROPIAS
                    }
                ),
                (
                    "Supervisor de Verificación",
                    "Verifica la identidad de los creadores",
                    "#8b5cf6", // Morado
                    "fa-id-card",
                    new[] {
                        PermisosPredefinidos.VERIFICACION_VER,
                        PermisosPredefinidos.VERIFICACION_APROBAR,
                        PermisosPredefinidos.VERIFICACION_RECHAZAR,
                        PermisosPredefinidos.USUARIOS_VER_BASICO,
                        PermisosPredefinidos.ESTADISTICAS_PROPIAS
                    }
                ),
                (
                    "Supervisor Senior",
                    "Supervisor con todos los permisos de supervisión",
                    "#ef4444", // Rojo
                    "fa-user-tie",
                    new[] {
                        PermisosPredefinidos.CONTENIDO_VER_COLA,
                        PermisosPredefinidos.CONTENIDO_APROBAR,
                        PermisosPredefinidos.CONTENIDO_RECHAZAR,
                        PermisosPredefinidos.CONTENIDO_CENSURAR,
                        PermisosPredefinidos.CONTENIDO_ESCALAR,
                        PermisosPredefinidos.CONTENIDO_VER_HISTORIAL,
                        PermisosPredefinidos.REPORTES_VER,
                        PermisosPredefinidos.REPORTES_RESOLVER,
                        PermisosPredefinidos.REPORTES_ESCALAR,
                        PermisosPredefinidos.VERIFICACION_VER,
                        PermisosPredefinidos.VERIFICACION_APROBAR,
                        PermisosPredefinidos.VERIFICACION_RECHAZAR,
                        PermisosPredefinidos.USUARIOS_VER_BASICO,
                        PermisosPredefinidos.USUARIOS_ADVERTIR,
                        PermisosPredefinidos.ESTADISTICAS_PROPIAS,
                        PermisosPredefinidos.ESTADISTICAS_EQUIPO
                    }
                )
            };
        }
    }
}
