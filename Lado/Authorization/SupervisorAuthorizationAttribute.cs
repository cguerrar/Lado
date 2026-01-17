using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Lado.Services;

namespace Lado.Authorization
{
    /// <summary>
    /// Atributo para autorizar acceso solo a supervisores
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class SupervisorAuthorizeAttribute : TypeFilterAttribute
    {
        public SupervisorAuthorizeAttribute() : base(typeof(SupervisorAuthorizationFilter))
        {
            Arguments = new object[] { Array.Empty<string>() };
        }

        public SupervisorAuthorizeAttribute(params string[] permisos) : base(typeof(SupervisorAuthorizationFilter))
        {
            Arguments = new object[] { permisos };
        }
    }

    /// <summary>
    /// Filtro que verifica si el usuario es supervisor y tiene los permisos requeridos
    /// </summary>
    public class SupervisorAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly IModeracionService _moderacionService;
        private readonly string[] _permisosRequeridos;

        public SupervisorAuthorizationFilter(IModeracionService moderacionService, string[] permisosRequeridos)
        {
            _moderacionService = moderacionService;
            _permisosRequeridos = permisosRequeridos;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // Verificar autenticación
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Los administradores siempre tienen acceso
            if (user.IsInRole("Admin"))
            {
                return;
            }

            // Verificar si es supervisor
            var esSupervisor = await _moderacionService.EsSupervisorAsync(userId);
            if (!esSupervisor)
            {
                context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
                return;
            }

            // Si se requieren permisos específicos, verificarlos
            if (_permisosRequeridos.Length > 0)
            {
                var permisos = await _moderacionService.ObtenerPermisosAsync(userId);

                // Verificar que tenga al menos uno de los permisos requeridos
                var tienePermiso = _permisosRequeridos.Any(p => permisos.Contains(p));

                if (!tienePermiso)
                {
                    context.Result = new RedirectToActionResult("AccesoDenegado", "Supervisor", null);
                    return;
                }
            }

            // Actualizar última actividad
            await _moderacionService.ActualizarActividadAsync(userId);
        }
    }

    /// <summary>
    /// Atributo para requerir acceso de Admin O Supervisor
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminOrSupervisorAttribute : TypeFilterAttribute
    {
        public AdminOrSupervisorAttribute() : base(typeof(AdminOrSupervisorFilter))
        {
        }
    }

    public class AdminOrSupervisorFilter : IAsyncAuthorizationFilter
    {
        private readonly IModeracionService _moderacionService;

        public AdminOrSupervisorFilter(IModeracionService moderacionService)
        {
            _moderacionService = moderacionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // Admin tiene acceso
            if (user.IsInRole("Admin"))
            {
                return;
            }

            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Supervisor tiene acceso
            var esSupervisor = await _moderacionService.EsSupervisorAsync(userId);
            if (esSupervisor)
            {
                return;
            }

            context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
        }
    }
}
