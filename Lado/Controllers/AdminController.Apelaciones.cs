using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // PANEL DE APELACIONES
        // ========================================

        public async Task<IActionResult> Apelaciones(
            string? estado = null,
            string? tipo = null,
            string? prioridad = null,
            string? busqueda = null,
            int pagina = 1)
        {
            var query = _context.Apelaciones
                .Include(a => a.Usuario)
                .Include(a => a.Contenido)
                .Include(a => a.Story)
                .Include(a => a.Administrador)
                .AsQueryable();

            // Filtrar por estado
            if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoApelacion>(estado, out var estadoEnum))
            {
                query = query.Where(a => a.Estado == estadoEnum);
            }

            // Filtrar por tipo de contenido
            if (!string.IsNullOrEmpty(tipo))
            {
                query = query.Where(a => a.TipoContenido == tipo);
            }

            // Filtrar por prioridad
            if (!string.IsNullOrEmpty(prioridad) && Enum.TryParse<PrioridadApelacion>(prioridad, out var prioridadEnum))
            {
                query = query.Where(a => a.Prioridad == prioridadEnum);
            }

            // Buscar por numero de referencia o usuario
            if (!string.IsNullOrEmpty(busqueda))
            {
                query = query.Where(a =>
                    a.NumeroReferencia.Contains(busqueda) ||
                    (a.Usuario != null && (a.Usuario.UserName!.Contains(busqueda) ||
                                          a.Usuario.NombreCompleto!.Contains(busqueda))));
            }

            // Ordenar: primero las pendientes de alta prioridad
            query = query
                .OrderBy(a => a.Estado == EstadoApelacion.Pendiente ? 0 :
                             a.Estado == EstadoApelacion.EnRevision ? 1 : 2)
                .ThenByDescending(a => a.Prioridad)
                .ThenByDescending(a => a.FechaCreacion);

            // Estadisticas
            ViewBag.TotalPendientes = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Pendiente);
            ViewBag.TotalEnRevision = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.EnRevision);
            ViewBag.TotalAprobadas = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Aprobada);
            ViewBag.TotalRechazadas = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Rechazada);
            ViewBag.TotalEscaladas = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Escalada);

            // Paginacion
            int porPagina = 20;
            var total = await query.CountAsync();
            var apelaciones = await query
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling(total / (double)porPagina);
            ViewBag.EstadoFiltro = estado;
            ViewBag.TipoFiltro = tipo;
            ViewBag.PrioridadFiltro = prioridad;
            ViewBag.Busqueda = busqueda;

            return View(apelaciones);
        }

        // Ver detalle de apelacion (Admin)
        public async Task<IActionResult> DetalleApelacion(int id)
        {
            var apelacion = await _context.Apelaciones
                .Include(a => a.Usuario)
                .Include(a => a.Contenido)
                    .ThenInclude(c => c!.Archivos)
                .Include(a => a.Story)
                .Include(a => a.Administrador)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (apelacion == null)
            {
                return NotFound();
            }

            // Marcar como en revision si esta pendiente
            if (apelacion.Estado == EstadoApelacion.Pendiente)
            {
                var admin = await _userManager.GetUserAsync(User);
                apelacion.Estado = EstadoApelacion.EnRevision;
                apelacion.AdministradorId = admin?.Id;
                await _context.SaveChangesAsync();
            }

            // Obtener historial de apelaciones anteriores del usuario
            ViewBag.ApelacionesAnteriores = await _context.Apelaciones
                .Where(a => a.UsuarioId == apelacion.UsuarioId && a.Id != id)
                .OrderByDescending(a => a.FechaCreacion)
                .Take(5)
                .ToListAsync();

            return View(apelacion);
        }

        // Resolver apelacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolverApelacion(int id, bool aprobar, string comentario, bool restaurarContenido)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var apelacion = await _context.Apelaciones
                .Include(a => a.Contenido)
                .Include(a => a.Story)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (apelacion == null)
            {
                return Json(new { success = false, message = "Apelacion no encontrada" });
            }

            apelacion.Estado = aprobar ? EstadoApelacion.Aprobada : EstadoApelacion.Rechazada;
            apelacion.FechaResolucion = DateTime.Now;
            apelacion.AdministradorId = admin.Id;
            apelacion.ResolucionComentario = comentario;
            apelacion.ContenidoRestaurado = false;

            // Si se aprueba y se quiere restaurar el contenido
            if (aprobar && restaurarContenido)
            {
                if (apelacion.Contenido != null)
                {
                    apelacion.Contenido.EstaActivo = true;
                    apelacion.Contenido.Censurado = false;
                    apelacion.Contenido.RazonCensura = null;
                    apelacion.ContenidoRestaurado = true;
                }
                else if (apelacion.Story != null)
                {
                    apelacion.Story.EstaActivo = true;
                    apelacion.ContenidoRestaurado = true;
                }
            }

            await _context.SaveChangesAsync();

            // Registrar en logs
            await _logEventoService.RegistrarEventoAsync(
                $"Apelacion {apelacion.NumeroReferencia} {(aprobar ? "APROBADA" : "RECHAZADA")}",
                CategoriaEvento.Admin,
                TipoLogEvento.Evento,
                admin.Id,
                admin.NombreCompleto ?? admin.UserName,
                $"Usuario: {apelacion.Usuario?.UserName}, Restaurado: {restaurarContenido}"
            );

            // Enviar notificación al usuario sobre la resolución
            if (!string.IsNullOrEmpty(apelacion.UsuarioId))
            {
                var tipoContenido = apelacion.TipoContenido.ToLower();
                var mensaje = aprobar
                    ? $"¡Buenas noticias! Tu apelación sobre tu {tipoContenido} ha sido aprobada."
                    : $"Tu apelación sobre tu {tipoContenido} ha sido revisada y no fue aprobada.";

                if (aprobar && restaurarContenido)
                {
                    mensaje += " Tu contenido ha sido restaurado.";
                }

                // Determinar URL de destino
                string? urlDestino = null;
                if (apelacion.ContenidoId.HasValue)
                {
                    urlDestino = $"/Feed/Detalle/{apelacion.ContenidoId}";
                }
                else if (apelacion.StoryId.HasValue)
                {
                    urlDestino = "/Stories/MisHistorias";
                }

                var notificacion = new Notificacion
                {
                    UsuarioId = apelacion.UsuarioId,
                    Tipo = TipoNotificacion.ApelacionResuelta,
                    Titulo = aprobar ? "Apelación Aprobada" : "Apelación Rechazada",
                    Mensaje = mensaje,
                    UrlDestino = urlDestino ?? "/Apelaciones/MisApelaciones",
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                _context.Notificaciones.Add(notificacion);
                await _context.SaveChangesAsync();
            }

            return Json(new {
                success = true,
                message = aprobar ? "Apelacion aprobada correctamente" : "Apelacion rechazada"
            });
        }

        // Escalar apelacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalarApelacion(int id, string motivo)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var apelacion = await _context.Apelaciones.FindAsync(id);
            if (apelacion == null)
            {
                return Json(new { success = false, message = "Apelacion no encontrada" });
            }

            apelacion.Estado = EstadoApelacion.Escalada;
            apelacion.Prioridad = PrioridadApelacion.Urgente;

            // Agregar nota de escalacion al comentario
            apelacion.ResolucionComentario = $"[ESCALADA por {admin.UserName}]: {motivo}";

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Apelacion {apelacion.NumeroReferencia} ESCALADA",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                admin.Id,
                admin.NombreCompleto ?? admin.UserName,
                $"Motivo: {motivo}"
            );

            return Json(new { success = true, message = "Apelacion escalada a revision superior" });
        }
    }
}
