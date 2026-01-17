using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Lado.Models;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // EXPORTACIONES AVANZADAS - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista de exportaciones
        /// </summary>
        [HttpGet]
        public IActionResult Exportaciones()
        {
            return View();
        }

        /// <summary>
        /// Exportar usuarios a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarUsuarios(
            DateTime? desde,
            DateTime? hasta,
            bool? soloCreadores,
            bool? soloVerificados)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (desde.HasValue)
                    query = query.Where(u => u.FechaRegistro >= desde.Value);
                if (hasta.HasValue)
                    query = query.Where(u => u.FechaRegistro <= hasta.Value);
                if (soloCreadores == true)
                    query = query.Where(u => u.EsCreador);
                if (soloVerificados == true)
                    query = query.Where(u => u.CreadorVerificado);

                var usuarios = await query.Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.NombreCompleto,
                    u.FechaRegistro,
                    u.EsCreador,
                    u.CreadorVerificado,
                    u.EstaActivo,
                    u.TotalGanancias,
                    u.Saldo
                }).ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("ID,Username,Email,NombreCompleto,FechaRegistro,EsCreador,EsVerificado,EstaActivo,TotalGanancias,Saldo");

                foreach (var u in usuarios)
                {
                    csv.AppendLine($"\"{u.Id}\",\"{u.UserName}\",\"{u.Email}\",\"{u.NombreCompleto ?? ""}\",\"{u.FechaRegistro:yyyy-MM-dd}\",{u.EsCreador},{u.CreadorVerificado},{u.EstaActivo},{u.TotalGanancias},{u.Saldo}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"usuarios_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error exportando usuarios");
                return BadRequest("Error al exportar");
            }
        }

        /// <summary>
        /// Exportar contenidos a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarContenidos(
            DateTime? desde,
            DateTime? hasta,
            TipoContenido? tipo)
        {
            try
            {
                var query = _context.Contenidos.Include(c => c.Usuario).AsQueryable();

                if (desde.HasValue)
                    query = query.Where(c => c.FechaPublicacion >= desde.Value);
                if (hasta.HasValue)
                    query = query.Where(c => c.FechaPublicacion <= hasta.Value);
                if (tipo.HasValue)
                    query = query.Where(c => c.TipoContenido == tipo.Value);

                var contenidos = await query.Select(c => new
                {
                    c.Id,
                    c.Descripcion,
                    Autor = c.Usuario != null ? c.Usuario.UserName : "",
                    c.TipoContenido,
                    c.FechaPublicacion,
                    c.EstaActivo,
                    c.EsGratis,
                    c.PrecioDesbloqueo,
                    c.NumeroLikes,
                    c.NumeroComentarios,
                    c.NumeroVistas
                }).ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("ID,Descripcion,Autor,Tipo,FechaCreacion,Activo,EsGratis,Precio,Likes,Comentarios,Vistas");

                foreach (var c in contenidos)
                {
                    csv.AppendLine($"{c.Id},\"{c.Descripcion?.Replace("\"", "\"\"") ?? ""}\",\"{c.Autor}\",{c.TipoContenido},{c.FechaPublicacion:yyyy-MM-dd},{c.EstaActivo},{c.EsGratis},{c.PrecioDesbloqueo ?? 0},{c.NumeroLikes},{c.NumeroComentarios},{c.NumeroVistas}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"contenidos_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error exportando contenidos");
                return BadRequest("Error al exportar");
            }
        }

        /// <summary>
        /// Exportar transacciones a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarTransacciones(
            DateTime? desde,
            DateTime? hasta,
            TipoTransaccion? tipo,
            EstadoTransaccion? estado)
        {
            try
            {
                var query = _context.Transacciones.Include(t => t.Usuario).AsQueryable();

                if (desde.HasValue)
                    query = query.Where(t => t.FechaTransaccion >= desde.Value);
                if (hasta.HasValue)
                    query = query.Where(t => t.FechaTransaccion <= hasta.Value);
                if (tipo.HasValue)
                    query = query.Where(t => t.TipoTransaccion == tipo.Value);
                if (estado.HasValue)
                    query = query.Where(t => t.EstadoTransaccion == estado.Value);

                var transacciones = await query.Select(t => new
                {
                    t.Id,
                    Usuario = t.Usuario != null ? t.Usuario.UserName : "",
                    t.TipoTransaccion,
                    t.EstadoTransaccion,
                    t.Monto,
                    t.FechaTransaccion,
                    t.Descripcion
                }).ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("ID,Usuario,Tipo,Estado,Monto,FechaCreacion,Descripcion");

                foreach (var t in transacciones)
                {
                    csv.AppendLine($"{t.Id},\"{t.Usuario}\",{t.TipoTransaccion},{t.EstadoTransaccion},{t.Monto},{t.FechaTransaccion:yyyy-MM-dd HH:mm},\"{t.Descripcion?.Replace("\"", "\"\"") ?? ""}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"transacciones_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error exportando transacciones");
                return BadRequest("Error al exportar");
            }
        }

        /// <summary>
        /// Exportar reportes a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarReportes(
            DateTime? desde,
            DateTime? hasta,
            string? estado)
        {
            try
            {
                var query = _context.Reportes
                    .Include(r => r.UsuarioReportador)
                    .AsQueryable();

                if (desde.HasValue)
                    query = query.Where(r => r.FechaReporte >= desde.Value);
                if (hasta.HasValue)
                    query = query.Where(r => r.FechaReporte <= hasta.Value);
                if (!string.IsNullOrEmpty(estado))
                    query = query.Where(r => r.Estado == estado);

                var reportes = await query.Select(r => new
                {
                    r.Id,
                    Reportador = r.UsuarioReportador != null ? r.UsuarioReportador.UserName : "",
                    r.TipoReporte,
                    r.Motivo,
                    r.Estado,
                    r.FechaReporte,
                    r.FechaResolucion
                }).ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("ID,Reportador,Tipo,Motivo,Estado,FechaReporte,FechaResolucion");

                foreach (var r in reportes)
                {
                    csv.AppendLine($"{r.Id},\"{r.Reportador}\",\"{r.TipoReporte}\",\"{r.Motivo?.Replace("\"", "\"\"") ?? ""}\",\"{r.Estado}\",{r.FechaReporte:yyyy-MM-dd},{r.FechaResolucion?.ToString("yyyy-MM-dd") ?? ""}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"reportes_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error exportando reportes");
                return BadRequest("Error al exportar");
            }
        }

        /// <summary>
        /// Exportar logs a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarLogs(
            DateTime? desde,
            DateTime? hasta,
            CategoriaEvento? categoria,
            TipoLogEvento? tipo)
        {
            try
            {
                var query = _context.LogEventos.AsQueryable();

                if (desde.HasValue)
                    query = query.Where(l => l.Fecha >= desde.Value);
                if (hasta.HasValue)
                    query = query.Where(l => l.Fecha <= hasta.Value);
                if (categoria.HasValue)
                    query = query.Where(l => l.Categoria == categoria.Value);
                if (tipo.HasValue)
                    query = query.Where(l => l.Tipo == tipo.Value);

                var logs = await query
                    .OrderByDescending(l => l.Fecha)
                    .Take(10000) // Limitar a 10k registros
                    .Select(l => new
                    {
                        l.Id,
                        l.Fecha,
                        l.Categoria,
                        l.Tipo,
                        l.Mensaje,
                        l.UsuarioId,
                        l.UsuarioNombre
                    }).ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("ID,Fecha,Categoria,Tipo,Mensaje,UsuarioId,UsuarioNombre");

                foreach (var l in logs)
                {
                    csv.AppendLine($"{l.Id},{l.Fecha:yyyy-MM-dd HH:mm:ss},{l.Categoria},{l.Tipo},\"{l.Mensaje?.Replace("\"", "\"\"") ?? ""}\",\"{l.UsuarioId ?? ""}\",\"{l.UsuarioNombre ?? ""}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"logs_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error exportando logs");
                return BadRequest("Error al exportar");
            }
        }
    }
}
