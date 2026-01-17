using Lado.Data;
using Lado.Models;
using Lado.Models.Moderacion;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Extensión del AdminController para gestión de supervisores
    /// </summary>
    public partial class AdminController
    {
        // ═══════════════════════════════════════════════════════════
        // GESTIÓN DE SUPERVISORES
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Lista de supervisores
        /// </summary>
        public async Task<IActionResult> Supervisores()
        {
            var supervisores = await _context.UsuariosSupervisor
                .Include(s => s.Usuario)
                .Include(s => s.RolSupervisor)
                .Include(s => s.AsignadoPor)
                .OrderByDescending(s => s.EstaActivo)
                .ThenBy(s => s.RolSupervisor.Nombre)
                .ThenBy(s => s.Usuario.UserName)
                .ToListAsync();

            var roles = await _context.RolesSupervisor
                .Where(r => r.Activo)
                .OrderBy(r => r.Nombre)
                .ToListAsync();

            // Estadísticas de la cola
            var estadisticasCola = new
            {
                TotalPendientes = await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.Pendiente),
                TotalEnRevision = await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.EnRevision),
                TotalEscalados = await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.Escalado),
                SupervisoresActivos = supervisores.Count(s => s.EstaActivo && s.EstaDisponible)
            };

            ViewBag.Roles = roles;
            ViewBag.EstadisticasCola = estadisticasCola;

            return View(supervisores);
        }

        /// <summary>
        /// Crear nuevo supervisor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSupervisor(string usuarioId, int rolId, string? turno, string? notas)
        {
            try
            {
                // Verificar que el usuario existe
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction(nameof(Supervisores));
                }

                // Verificar que no sea ya supervisor
                var existente = await _context.UsuariosSupervisor
                    .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.EstaActivo);

                if (existente != null)
                {
                    TempData["Error"] = "Este usuario ya es supervisor";
                    return RedirectToAction(nameof(Supervisores));
                }

                // Verificar que el rol existe
                var rol = await _context.RolesSupervisor.FindAsync(rolId);
                if (rol == null)
                {
                    TempData["Error"] = "Rol no encontrado";
                    return RedirectToAction(nameof(Supervisores));
                }

                var admin = await _userManager.GetUserAsync(User);

                var nuevoSupervisor = new UsuarioSupervisor
                {
                    UsuarioId = usuarioId,
                    RolSupervisorId = rolId,
                    EstaActivo = true,
                    EstaDisponible = true,
                    FechaAsignacion = DateTime.UtcNow,
                    AsignadoPorId = admin?.Id,
                    Turno = turno,
                    Notas = notas
                };

                _context.UsuariosSupervisor.Add(nuevoSupervisor);
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Supervisor creado: {usuario.UserName} con rol {rol.Nombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    admin?.Id,
                    admin?.UserName);

                TempData["Success"] = $"Supervisor {usuario.UserName} creado correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear supervisor");
                TempData["Error"] = "Error al crear el supervisor";
            }

            return RedirectToAction(nameof(Supervisores));
        }

        /// <summary>
        /// Desactivar supervisor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarSupervisor(int id)
        {
            var supervisor = await _context.UsuariosSupervisor
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supervisor == null)
            {
                return Json(new { success = false, message = "Supervisor no encontrado" });
            }

            supervisor.EstaActivo = false;
            supervisor.FechaDesactivacion = DateTime.UtcNow;

            // Liberar items asignados
            var itemsAsignados = await _context.ColaModeracion
                .Where(c => c.SupervisorAsignadoId == supervisor.UsuarioId && c.Estado == EstadoModeracion.EnRevision)
                .ToListAsync();

            foreach (var item in itemsAsignados)
            {
                item.Estado = EstadoModeracion.Pendiente;
                item.SupervisorAsignadoId = null;
                item.FechaAsignacion = null;
            }

            await _context.SaveChangesAsync();

            var admin = await _userManager.GetUserAsync(User);
            await _logEventoService.RegistrarEventoAsync(
                $"Supervisor desactivado: {supervisor.Usuario?.UserName}",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                admin?.Id,
                admin?.UserName);

            return Json(new { success = true });
        }

        /// <summary>
        /// Reactivar supervisor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarSupervisor(int id)
        {
            var supervisor = await _context.UsuariosSupervisor.FindAsync(id);

            if (supervisor == null)
            {
                return Json(new { success = false, message = "Supervisor no encontrado" });
            }

            supervisor.EstaActivo = true;
            supervisor.EstaDisponible = true;
            supervisor.FechaDesactivacion = null;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        /// <summary>
        /// Cambiar rol de supervisor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarRolSupervisor(int supervisorId, int nuevoRolId)
        {
            var supervisor = await _context.UsuariosSupervisor.FindAsync(supervisorId);

            if (supervisor == null)
            {
                return Json(new { success = false, message = "Supervisor no encontrado" });
            }

            var rol = await _context.RolesSupervisor.FindAsync(nuevoRolId);
            if (rol == null)
            {
                return Json(new { success = false, message = "Rol no encontrado" });
            }

            supervisor.RolSupervisorId = nuevoRolId;
            await _context.SaveChangesAsync();

            return Json(new { success = true, rolNombre = rol.Nombre });
        }

        /// <summary>
        /// Buscar usuarios para asignar como supervisores
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarUsuariosParaSupervisor(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new List<object>());
            }

            // Obtener IDs de usuarios que ya son supervisores activos
            var supervisoresActivos = await _context.UsuariosSupervisor
                .Where(s => s.EstaActivo)
                .Select(s => s.UsuarioId)
                .ToListAsync();

            var usuarios = await _context.Users
                .Where(u => !supervisoresActivos.Contains(u.Id) &&
                           (u.UserName!.Contains(query) ||
                            u.Email!.Contains(query) ||
                            (u.NombreCompleto != null && u.NombreCompleto.Contains(query))))
                .Take(10)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.NombreCompleto,
                    u.FotoPerfil
                })
                .ToListAsync();

            return Json(usuarios);
        }

        // ═══════════════════════════════════════════════════════════
        // GESTIÓN DE ROLES DE SUPERVISOR
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Ver y editar roles de supervisor
        /// </summary>
        public async Task<IActionResult> RolesSupervisor()
        {
            var roles = await _context.RolesSupervisor
                .Include(r => r.RolesPermisos)
                    .ThenInclude(rp => rp.PermisoSupervisor)
                .Include(r => r.Usuarios)
                .OrderBy(r => r.Nombre)
                .ToListAsync();

            var permisos = await _context.PermisosSupervisor
                .Where(p => p.Activo)
                .OrderBy(p => p.Modulo)
                .ThenBy(p => p.Orden)
                .ToListAsync();

            ViewBag.Permisos = permisos;

            return View(roles);
        }

        /// <summary>
        /// Crear nuevo rol de supervisor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearRolSupervisor(string nombre, string? descripcion, string colorBadge, string icono, int[] permisos)
        {
            try
            {
                var rol = new RolSupervisor
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    ColorBadge = colorBadge,
                    Icono = icono,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.RolesSupervisor.Add(rol);
                await _context.SaveChangesAsync();

                // Asignar permisos
                foreach (var permisoId in permisos)
                {
                    _context.RolesSupervisorPermisos.Add(new RolSupervisorPermiso
                    {
                        RolSupervisorId = rol.Id,
                        PermisoSupervisorId = permisoId
                    });
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Rol '{nombre}' creado correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear rol de supervisor");
                TempData["Error"] = "Error al crear el rol";
            }

            return RedirectToAction(nameof(RolesSupervisor));
        }

        /// <summary>
        /// Actualizar permisos de un rol
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPermisosRol(int rolId, int[] permisos)
        {
            try
            {
                // Eliminar permisos actuales
                var permisosActuales = await _context.RolesSupervisorPermisos
                    .Where(rp => rp.RolSupervisorId == rolId)
                    .ToListAsync();

                _context.RolesSupervisorPermisos.RemoveRange(permisosActuales);

                // Agregar nuevos permisos
                foreach (var permisoId in permisos)
                {
                    _context.RolesSupervisorPermisos.Add(new RolSupervisorPermiso
                    {
                        RolSupervisorId = rolId,
                        PermisoSupervisorId = permisoId
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar permisos del rol");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // COLA DE MODERACIÓN (VISTA ADMIN)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Ver cola de moderación escalada
        /// </summary>
        public async Task<IActionResult> ColaEscalada()
        {
            var colaEscalada = await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Include(c => c.Contenido)
                    .ThenInclude(c => c.Archivos)
                .Include(c => c.SupervisorAsignado)
                .Include(c => c.Decisiones)
                    .ThenInclude(d => d.Supervisor)
                .Where(c => c.Estado == EstadoModeracion.Escalado)
                .OrderBy(c => c.Prioridad)
                .ThenBy(c => c.FechaCreacion)
                .ToListAsync();

            return View(colaEscalada);
        }

        /// <summary>
        /// Resolver item escalado (aprobar/rechazar definitivamente)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolverEscalado(int colaId, string accion, string? comentario)
        {
            var item = await _context.ColaModeracion
                .Include(c => c.Contenido)
                .FirstOrDefaultAsync(c => c.Id == colaId);

            if (item == null)
            {
                return Json(new { success = false, message = "Item no encontrado" });
            }

            var admin = await _userManager.GetUserAsync(User);

            switch (accion.ToLower())
            {
                case "aprobar":
                    item.Estado = EstadoModeracion.Aprobado;
                    item.FechaResolucion = DateTime.UtcNow;
                    item.DecisionFinal = TipoDecisionModeracion.Aprobado;
                    break;

                case "rechazar":
                    item.Estado = EstadoModeracion.Rechazado;
                    item.FechaResolucion = DateTime.UtcNow;
                    item.DecisionFinal = TipoDecisionModeracion.Rechazado;
                    item.Contenido.EstaActivo = false;
                    break;

                case "censurar":
                    item.Estado = EstadoModeracion.Censurado;
                    item.FechaResolucion = DateTime.UtcNow;
                    item.DecisionFinal = TipoDecisionModeracion.Censurado;
                    item.Contenido.Censurado = true;
                    item.Contenido.RazonCensura = comentario;
                    break;
            }

            // Registrar decisión del admin
            _context.DecisionesModeracion.Add(new DecisionModeracion
            {
                ColaModeracionId = colaId,
                SupervisorId = admin!.Id,
                Decision = item.DecisionFinal ?? TipoDecisionModeracion.Aprobado,
                Comentario = $"[ADMIN] {comentario}",
                FechaDecision = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Item escalado {colaId} resuelto: {accion}",
                CategoriaEvento.Admin,
                TipoLogEvento.Evento,
                admin.Id,
                admin.UserName,
                comentario);

            return Json(new { success = true });
        }

        // ═══════════════════════════════════════════════════════════
        // MÉTRICAS DE SUPERVISORES
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Ver métricas de todos los supervisores
        /// </summary>
        public async Task<IActionResult> MetricasSupervisores(int dias = 7)
        {
            var desde = DateTime.UtcNow.Date.AddDays(-dias);

            var metricas = await _context.MetricasSupervisor
                .Include(m => m.Supervisor)
                .Where(m => m.Fecha >= desde)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            // Agrupar por supervisor
            var resumenPorSupervisor = metricas
                .GroupBy(m => m.SupervisorId)
                .Select(g => new
                {
                    SupervisorId = g.Key,
                    Supervisor = g.First().Supervisor,
                    TotalRevisados = g.Sum(m => m.TotalRevisados),
                    Aprobados = g.Sum(m => m.Aprobados),
                    Rechazados = g.Sum(m => m.Rechazados),
                    Escalados = g.Sum(m => m.Escalados),
                    TiempoPromedio = g.Average(m => m.TiempoPromedioSegundos),
                    DiasActivos = g.Count()
                })
                .OrderByDescending(x => x.TotalRevisados)
                .ToList();

            ViewBag.Dias = dias;
            ViewBag.ResumenPorSupervisor = resumenPorSupervisor;
            ViewBag.MetricasDiarias = metricas;

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        // INICIALIZACIÓN DE PERMISOS Y ROLES
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Inicializar permisos y roles predefinidos
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InicializarSistemaSupervisores()
        {
            try
            {
                // Verificar si ya están creados
                if (await _context.PermisosSupervisor.AnyAsync())
                {
                    TempData["Info"] = "El sistema de supervisores ya esta inicializado";
                    return RedirectToAction(nameof(Supervisores));
                }

                // Crear permisos
                var permisos = PermisosPredefinidos.ObtenerTodos();
                _context.PermisosSupervisor.AddRange(permisos);
                await _context.SaveChangesAsync();

                // Crear roles predefinidos
                var rolesPredefinidos = RolesPredefinidos.ObtenerRolesPredefinidos();

                foreach (var (nombre, descripcion, color, icono, codigosPermisos) in rolesPredefinidos)
                {
                    var rol = new RolSupervisor
                    {
                        Nombre = nombre,
                        Descripcion = descripcion,
                        ColorBadge = color,
                        Icono = icono,
                        FechaCreacion = DateTime.UtcNow
                    };

                    _context.RolesSupervisor.Add(rol);
                    await _context.SaveChangesAsync();

                    // Asignar permisos al rol
                    foreach (var codigo in codigosPermisos)
                    {
                        var permiso = await _context.PermisosSupervisor
                            .FirstOrDefaultAsync(p => p.Codigo == codigo);

                        if (permiso != null)
                        {
                            _context.RolesSupervisorPermisos.Add(new RolSupervisorPermiso
                            {
                                RolSupervisorId = rol.Id,
                                PermisoSupervisorId = permiso.Id
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                var admin = await _userManager.GetUserAsync(User);
                await _logEventoService.RegistrarEventoAsync(
                    "Sistema de supervisores inicializado",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    admin?.Id,
                    admin?.UserName);

                TempData["Success"] = "Sistema de supervisores inicializado correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar sistema de supervisores");
                TempData["Error"] = "Error al inicializar el sistema";
            }

            return RedirectToAction(nameof(Supervisores));
        }
    }
}
