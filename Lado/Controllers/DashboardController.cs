using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Cargar datos del usuario (todos son creadores ahora)
            await CargarDatosCreador(usuario);
            await CargarDatosFan(usuario);

            return View(usuario);
        }

        private async Task CargarDatosCreador(ApplicationUser usuario)
        {
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            ViewBag.IngresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            ViewBag.Suscriptores = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            var inicioSemana = DateTime.Now.AddDays(-7);
            ViewBag.NuevosSeguidores = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id &&
                                 s.FechaInicio >= inicioSemana);

            ViewBag.Contenidos = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id);

            // Conteo por tipo de lado
            ViewBag.TotalLadoA = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.TipoLado == TipoLado.LadoA);

            ViewBag.TotalLadoB = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.TipoLado == TipoLado.LadoB);

            ViewBag.Likes = await _context.Set<Like>()
                .Where(l => l.Contenido.UsuarioId == usuario.Id)
                .CountAsync();

            if (ViewBag.Contenidos > 0 && ViewBag.Suscriptores > 0)
            {
                var totalInteracciones = ViewBag.Likes;
                var posiblesInteracciones = ViewBag.Suscriptores * ViewBag.Contenidos;
                ViewBag.Engagement = Math.Round((double)totalInteracciones / posiblesInteracciones * 100, 1);
            }
            else
            {
                ViewBag.Engagement = 0;
            }

            ViewBag.Actividades = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado")
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(5)
                .ToListAsync();

            // Últimos seguidores LadoA
            ViewBag.UltimosSeguidoresLadoA = await _context.Suscripciones
                .Where(s => s.CreadorId == usuario.Id && s.EstaActiva && s.TipoLado == TipoLado.LadoA)
                .Include(s => s.Fan)
                .OrderByDescending(s => s.FechaInicio)
                .Take(5)
                .Select(s => new {
                    Id = s.Fan.Id,
                    UserName = s.Fan.UserName,
                    NombreCompleto = s.Fan.NombreCompleto,
                    FotoPerfil = s.Fan.FotoPerfil,
                    FechaSuscripcion = s.FechaInicio
                })
                .ToListAsync();

            // Últimos seguidores LadoB
            ViewBag.UltimosSeguidoresLadoB = await _context.Suscripciones
                .Where(s => s.CreadorId == usuario.Id && s.EstaActiva && s.TipoLado == TipoLado.LadoB)
                .Include(s => s.Fan)
                .OrderByDescending(s => s.FechaInicio)
                .Take(5)
                .Select(s => new {
                    Id = s.Fan.Id,
                    UserName = s.Fan.UserName,
                    NombreCompleto = s.Fan.NombreCompleto,
                    FotoPerfil = s.Fan.FotoPerfil,
                    FechaSuscripcion = s.FechaInicio
                })
                .ToListAsync();

            // OPTIMIZADO: Cálculo de ingresos por semana (últimas 4 semanas) en UNA sola consulta
            var hoy = DateTime.Now;
            var hace4Semanas = hoy.AddDays(-28);

            // Una sola consulta para todas las transacciones de las últimas 4 semanas
            var transacciones4Semanas = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                           t.TipoTransaccion != TipoTransaccion.Retiro &&
                           t.EstadoPago == "Completado" &&
                           t.FechaTransaccion >= hace4Semanas)
                .Select(t => new { t.FechaTransaccion, t.Monto })
                .ToListAsync();

            // Agrupar en memoria por semana
            var ingresosPorSemana = new decimal[4];
            foreach (var t in transacciones4Semanas)
            {
                var diasAtras = (hoy - t.FechaTransaccion).Days;
                var semanaIndex = Math.Min(3, diasAtras / 7);
                ingresosPorSemana[3 - semanaIndex] += t.Monto; // Invertir para orden cronológico
            }

            ViewBag.IngresosSemana1 = ingresosPorSemana[0];
            ViewBag.IngresosSemana2 = ingresosPorSemana[1];
            ViewBag.IngresosSemana3 = ingresosPorSemana[2];
            ViewBag.IngresosSemana4 = ingresosPorSemana[3];
        }

        private async Task CargarDatosFan(ApplicationUser usuario)
        {
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // ✅ Contador de suscripciones activas
            ViewBag.SuscripcionesActivas = await _context.Suscripciones
                .CountAsync(s => s.FanId == usuario.Id && s.EstaActiva);

            // Lista completa de suscripciones
            ViewBag.Suscripciones = await _context.Suscripciones
                .Where(s => s.FanId == usuario.Id && s.EstaActiva)
                .Include(s => s.Creador)
                .OrderByDescending(s => s.FechaInicio)
                .ToListAsync();

            // Opción 1: Solo suscripciones que iniciaron este mes
            var gastoSuscripcionesNuevas = await _context.Suscripciones
                .Where(s => s.FanId == usuario.Id &&
                           s.EstaActiva &&
                           s.FechaInicio >= inicioMes)
                .SumAsync(s => (decimal?)s.PrecioMensual) ?? 0;

            // Opción 2: Solo transacciones de SUSCRIPCIÓN
            var gastoTransacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                           t.TipoTransaccion == TipoTransaccion.Suscripcion &&
                           t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // ⭐ CORREGIDO: Cambiar de GastoMes a GastadoEsteMes
            ViewBag.GastadoEsteMes = Math.Max(gastoSuscripcionesNuevas, gastoTransacciones);

            ViewBag.ContenidoVisto = await _context.Set<Like>()
                .Where(l => l.UsuarioId == usuario.Id)
                .CountAsync();

            var actividadesFan = await _context.Suscripciones
                .Where(s => s.FanId == usuario.Id)
                .Include(s => s.Creador)
                .OrderByDescending(s => s.FechaInicio)
                .Take(5)
                .Select(s => new
                {
                    Descripcion = $"Te suscribiste a {s.Creador.NombreCompleto}",
                    Tipo = "suscripcion",
                    Fecha = s.FechaInicio
                })
                .ToListAsync();

            ViewBag.ActividadesFan = actividadesFan;
        }
    }
}