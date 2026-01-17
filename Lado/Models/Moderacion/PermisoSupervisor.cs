using System.ComponentModel.DataAnnotations;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Define un permiso específico que puede asignarse a roles de supervisor
    /// </summary>
    public class PermisoSupervisor
    {
        public int Id { get; set; }

        /// <summary>Código único del permiso (ej: "contenido.aprobar")</summary>
        [Required]
        [MaxLength(100)]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre legible del permiso</summary>
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Descripción detallada del permiso</summary>
        [MaxLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>Módulo al que pertenece el permiso</summary>
        public ModuloSupervisor Modulo { get; set; }

        /// <summary>Orden de visualización</summary>
        public int Orden { get; set; }

        /// <summary>Si el permiso está activo</summary>
        public bool Activo { get; set; } = true;

        // Navegación
        public virtual ICollection<RolSupervisorPermiso> RolesPermisos { get; set; } = new List<RolSupervisorPermiso>();
    }

    /// <summary>
    /// Permisos predefinidos del sistema
    /// </summary>
    public static class PermisosPredefinidos
    {
        // Módulo Contenido
        public const string CONTENIDO_VER_COLA = "contenido.ver_cola";
        public const string CONTENIDO_APROBAR = "contenido.aprobar";
        public const string CONTENIDO_RECHAZAR = "contenido.rechazar";
        public const string CONTENIDO_CENSURAR = "contenido.censurar";
        public const string CONTENIDO_ESCALAR = "contenido.escalar";
        public const string CONTENIDO_VER_HISTORIAL = "contenido.ver_historial";

        // Módulo Reportes
        public const string REPORTES_VER = "reportes.ver";
        public const string REPORTES_RESOLVER = "reportes.resolver";
        public const string REPORTES_ESCALAR = "reportes.escalar";

        // Módulo Verificación
        public const string VERIFICACION_VER = "verificacion.ver";
        public const string VERIFICACION_APROBAR = "verificacion.aprobar";
        public const string VERIFICACION_RECHAZAR = "verificacion.rechazar";

        // Módulo Usuarios (limitado)
        public const string USUARIOS_VER_BASICO = "usuarios.ver_basico";
        public const string USUARIOS_ADVERTIR = "usuarios.advertir";

        // Módulo Estadísticas
        public const string ESTADISTICAS_PROPIAS = "estadisticas.propias";
        public const string ESTADISTICAS_EQUIPO = "estadisticas.equipo";

        /// <summary>
        /// Lista de todos los permisos para inicialización
        /// </summary>
        public static List<PermisoSupervisor> ObtenerTodos()
        {
            return new List<PermisoSupervisor>
            {
                // Contenido
                new() { Codigo = CONTENIDO_VER_COLA, Nombre = "Ver cola de moderación", Modulo = ModuloSupervisor.Contenido, Orden = 1 },
                new() { Codigo = CONTENIDO_APROBAR, Nombre = "Aprobar contenido", Modulo = ModuloSupervisor.Contenido, Orden = 2 },
                new() { Codigo = CONTENIDO_RECHAZAR, Nombre = "Rechazar contenido", Modulo = ModuloSupervisor.Contenido, Orden = 3 },
                new() { Codigo = CONTENIDO_CENSURAR, Nombre = "Censurar contenido", Modulo = ModuloSupervisor.Contenido, Orden = 4 },
                new() { Codigo = CONTENIDO_ESCALAR, Nombre = "Escalar a administrador", Modulo = ModuloSupervisor.Contenido, Orden = 5 },
                new() { Codigo = CONTENIDO_VER_HISTORIAL, Nombre = "Ver historial de decisiones", Modulo = ModuloSupervisor.Contenido, Orden = 6 },

                // Reportes
                new() { Codigo = REPORTES_VER, Nombre = "Ver reportes", Modulo = ModuloSupervisor.Reportes, Orden = 1 },
                new() { Codigo = REPORTES_RESOLVER, Nombre = "Resolver reportes", Modulo = ModuloSupervisor.Reportes, Orden = 2 },
                new() { Codigo = REPORTES_ESCALAR, Nombre = "Escalar reportes", Modulo = ModuloSupervisor.Reportes, Orden = 3 },

                // Verificación
                new() { Codigo = VERIFICACION_VER, Nombre = "Ver solicitudes de verificación", Modulo = ModuloSupervisor.Verificacion, Orden = 1 },
                new() { Codigo = VERIFICACION_APROBAR, Nombre = "Aprobar verificaciones", Modulo = ModuloSupervisor.Verificacion, Orden = 2 },
                new() { Codigo = VERIFICACION_RECHAZAR, Nombre = "Rechazar verificaciones", Modulo = ModuloSupervisor.Verificacion, Orden = 3 },

                // Usuarios
                new() { Codigo = USUARIOS_VER_BASICO, Nombre = "Ver info básica de usuarios", Modulo = ModuloSupervisor.Usuarios, Orden = 1 },
                new() { Codigo = USUARIOS_ADVERTIR, Nombre = "Enviar advertencias", Modulo = ModuloSupervisor.Usuarios, Orden = 2 },

                // Estadísticas
                new() { Codigo = ESTADISTICAS_PROPIAS, Nombre = "Ver estadísticas propias", Modulo = ModuloSupervisor.Estadisticas, Orden = 1 },
                new() { Codigo = ESTADISTICAS_EQUIPO, Nombre = "Ver estadísticas del equipo", Modulo = ModuloSupervisor.Estadisticas, Orden = 2 },
            };
        }
    }
}
