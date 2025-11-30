using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class BilleteraController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public BilleteraController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Billetera
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

          
            // Ingresos este mes (todas las transacciones excepto retiros)
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.IngresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.FechaTransaccion >= inicioMes &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Crecimiento mes anterior
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.FechaTransaccion >= inicioMesAnterior &&
                            t.FechaTransaccion < inicioMes &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            if (ingresosMesAnterior > 0)
            {
                var crecimiento = ((ViewBag.IngresosEsteMes - ingresosMesAnterior) / ingresosMesAnterior) * 100;
                ViewBag.CrecimientoMes = Math.Round(crecimiento, 1);
            }
            else
            {
                ViewBag.CrecimientoMes = 0;
            }

            // Total retirado
            ViewBag.TotalRetirado = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion == TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Retiros pendientes
            ViewBag.RetirosPendientes = await _context.Transacciones
                .CountAsync(t => t.UsuarioId == usuario.Id &&
                                 t.TipoTransaccion == TipoTransaccion.Retiro &&
                                 t.EstadoPago == "Pendiente");

            // ✅ CORREGIDO: Usar DTO en lugar de tipo anónimo
            ViewBag.Transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(10)
                .Select(t => new TransaccionDto
                {
                    Id = t.Id,
                    FechaTransaccion = t.FechaTransaccion,
                    Tipo = t.TipoTransaccion == TipoTransaccion.Retiro ? "Retiro" : "Ingreso",
                    Descripcion = t.Descripcion,
                    Monto = t.Monto,
                    Estado = t.EstadoPago ?? "Completado",
                    TipoTransaccion = t.TipoTransaccion
                })
                .ToListAsync();

            // Próximo pago estimado (primer día del siguiente mes)
            ViewBag.ProximoPago = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);

            // Monto estimado basado en suscripciones activas
            var suscripcionesActivas = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            ViewBag.MontoEstimado = suscripcionesActivas * usuario.PrecioSuscripcion;

            // Método de pago configurado (ejemplo, deberías tener una tabla para esto)
            ViewBag.MetodoPago = "Transferencia Bancaria"; // Ejemplo
            ViewBag.CuentaBancaria = "**** **** **** 1234"; // Ejemplo

            return View(usuario);
        }

        // POST: /Billetera/SolicitarRetiro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarRetiro(decimal monto, string metodoPago, string? detalles)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

          
            // Validaciones
            if (monto <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a 0";
                return RedirectToAction(nameof(Index));
            }

            if (monto > usuario.Saldo)
            {
                TempData["Error"] = "No tienes saldo suficiente para este retiro";
                return RedirectToAction(nameof(Index));
            }

            // Crear transacción de retiro
            var transaccion = new Transaccion
            {
                UsuarioId = usuario.Id,
                Monto = monto,
                TipoTransaccion = TipoTransaccion.Retiro,
                Descripcion = $"Retiro vía {metodoPago}",
                EstadoPago = "Pendiente",
                FechaTransaccion = DateTime.Now
            };

            _context.Transacciones.Add(transaccion);

            // Restar del saldo disponible
            usuario.Saldo -= monto;
            _context.Users.Update(usuario);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Retiro de ${monto:N2} solicitado correctamente. Se procesará en 3-5 días hábiles.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Billetera/ActualizarMetodoPago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarMetodoPago(
            string tipoCuenta,
            string nombreBanco,
            string numeroCuenta,
            string nombreTitular,
            string identificacion)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

  
            TempData["Success"] = "Método de pago actualizado correctamente";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Billetera/HistorialCompleto
        public async Task<IActionResult> HistorialCompleto()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            ViewBag.SaldoActual = usuario.Saldo;
            return View(transacciones);
        }

        // GET: /Billetera/HistorialTransacciones
        public async Task<IActionResult> HistorialTransacciones(int pagina = 1, string tipo = "todas")
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

      
            int itemsPorPagina = 20;

            var query = _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id);

            if (tipo != "todas")
            {
                if (tipo == "retiro")
                {
                    query = query.Where(t => t.TipoTransaccion == TipoTransaccion.Retiro);
                }
                else
                {
                    query = query.Where(t => t.TipoTransaccion != TipoTransaccion.Retiro);
                }
            }

            var totalItems = await query.CountAsync();
            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * itemsPorPagina)
                .Take(itemsPorPagina)
                .ToListAsync();

            ViewBag.Transacciones = transacciones;
            ViewBag.TotalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);
            ViewBag.PaginaActual = pagina;
            ViewBag.TipoFiltro = tipo;

            return View(usuario);
        }
    }
}