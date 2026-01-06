using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize]
    public class FinanzasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IReferidosService _referidosService;
        private readonly IRachasService _rachasService;
        private readonly ILogger<FinanzasController> _logger;

        public FinanzasController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILadoCoinsService ladoCoinsService,
            IReferidosService referidosService,
            IRachasService rachasService,
            ILogger<FinanzasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _ladoCoinsService = ladoCoinsService;
            _referidosService = referidosService;
            _rachasService = rachasService;
            _logger = logger;
        }

        /// <summary>
        /// Vista unificada de Finanzas: Billetera + LadoCoins
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            // ========== DATOS DE LADO COINS ==========
            var saldoLC = await _ladoCoinsService.ObtenerOCrearSaldoAsync(usuario.Id);
            var racha = await _rachasService.ObtenerOCrearRachaAsync(usuario.Id);
            var montoPorVencer = await _ladoCoinsService.ObtenerMontoPorVencerAsync(usuario.Id);
            var totalReferidos = await _referidosService.ContarReferidosAsync(usuario.Id);

            // Código de referido
            var codigoReferido = usuario.CodigoReferido;
            if (string.IsNullOrEmpty(codigoReferido))
            {
                codigoReferido = await _referidosService.GenerarCodigoReferidoAsync(usuario.Id);
            }

            // Contadores diarios
            var contadores = await _rachasService.ObtenerContadoresHoyAsync(usuario.Id);

            // ========== DATOS DE BILLETERA (DINERO REAL) ==========
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var ingresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Crecimiento del mes
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMesAnterior &&
                            t.FechaTransaccion < inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var crecimientoMes = ingresosMesAnterior > 0
                ? Math.Round(((ingresosEsteMes - ingresosMesAnterior) / ingresosMesAnterior) * 100, 1)
                : 0;

            // Total retirado
            var totalRetirado = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion == TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Retiros pendientes
            var retirosPendientes = await _context.Transacciones
                .CountAsync(t => t.UsuarioId == usuario.Id &&
                                t.TipoTransaccion == TipoTransaccion.Retiro &&
                                t.EstadoPago == "Pendiente");

            // Transacciones recientes (últimas 10)
            var transaccionesRecientes = await _context.Transacciones
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

            // Transacciones LadoCoins recientes
            var transaccionesLC = await _context.TransaccionesLadoCoins
                .Where(t => t.UsuarioId == usuario.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(10)
                .ToListAsync();

            // ========== CONSTRUIR VIEWMODEL ==========
            var model = new FinanzasViewModel
            {
                // Dinero real
                SaldoReal = usuario.Saldo,
                IngresosEsteMes = ingresosEsteMes,
                CrecimientoMes = crecimientoMes,
                TotalRetirado = totalRetirado,
                RetirosPendientes = retirosPendientes,
                TransaccionesRecientes = transaccionesRecientes,

                // LadoCoins
                SaldoLadoCoins = saldoLC.SaldoDisponible,
                SaldoPorVencer = saldoLC.SaldoPorVencer,
                MontoPorVencer7Dias = montoPorVencer,
                TotalLCGanado = saldoLC.TotalGanado,
                TotalLCGastado = saldoLC.TotalGastado,
                TransaccionesLC = transaccionesLC,

                // Racha
                RachaActual = racha.RachaActiva() ? racha.RachaActual : 0,
                RachaMaxima = racha.RachaMaxima,
                LikesHoy = contadores.likes,
                ComentariosHoy = contadores.comentarios,
                ContenidosHoy = contadores.contenidos,
                Premio5LikesHoy = racha.Premio5LikesHoy,
                Premio3ComentariosHoy = racha.Premio3ComentariosHoy,
                PremioContenidoHoy = racha.PremioContenidoHoy,
                PremioLoginHoy = racha.PremioLoginHoy,

                // Referidos
                CodigoReferido = codigoReferido,
                TotalReferidos = totalReferidos
            };

            return View(model);
        }
    }

    public class FinanzasViewModel
    {
        // Dinero Real
        public decimal SaldoReal { get; set; }
        public decimal IngresosEsteMes { get; set; }
        public decimal CrecimientoMes { get; set; }
        public decimal TotalRetirado { get; set; }
        public int RetirosPendientes { get; set; }
        public List<TransaccionDto> TransaccionesRecientes { get; set; } = new();

        // LadoCoins
        public decimal SaldoLadoCoins { get; set; }
        public decimal SaldoPorVencer { get; set; }
        public decimal MontoPorVencer7Dias { get; set; }
        public decimal TotalLCGanado { get; set; }
        public decimal TotalLCGastado { get; set; }
        public List<TransaccionLadoCoin> TransaccionesLC { get; set; } = new();

        // Racha
        public int RachaActual { get; set; }
        public int RachaMaxima { get; set; }
        public int LikesHoy { get; set; }
        public int ComentariosHoy { get; set; }
        public int ContenidosHoy { get; set; }
        public bool Premio5LikesHoy { get; set; }
        public bool Premio3ComentariosHoy { get; set; }
        public bool PremioContenidoHoy { get; set; }
        public bool PremioLoginHoy { get; set; }

        // Referidos
        public string? CodigoReferido { get; set; }
        public int TotalReferidos { get; set; }
    }
}
