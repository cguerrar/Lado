using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IBusquedaGlobalService
    {
        Task<BusquedaGlobalResultado> BuscarAsync(string termino, int limite = 10);
        Task<List<ResultadoBusqueda>> BuscarUsuariosAsync(string termino, int limite = 20);
        Task<List<ResultadoBusqueda>> BuscarContenidosAsync(string termino, int limite = 20);
        Task<List<ResultadoBusqueda>> BuscarReportesAsync(string termino, int limite = 20);
    }

    public class BusquedaGlobalService : IBusquedaGlobalService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BusquedaGlobalService> _logger;

        public BusquedaGlobalService(
            ApplicationDbContext context,
            ILogger<BusquedaGlobalService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BusquedaGlobalResultado> BuscarAsync(string termino, int limite = 10)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return new BusquedaGlobalResultado();

            var resultado = new BusquedaGlobalResultado();

            try
            {
                // Buscar en paralelo
                var tareas = new List<Task>
                {
                    BuscarUsuariosInternoAsync(termino, limite, resultado),
                    BuscarContenidosInternoAsync(termino, limite, resultado),
                    BuscarReportesInternoAsync(termino, limite, resultado),
                    BuscarApelacionesInternoAsync(termino, limite, resultado),
                    BuscarTransaccionesInternoAsync(termino, limite, resultado)
                };

                await Task.WhenAll(tareas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BusquedaGlobal] Error en búsqueda: {Termino}", termino);
            }

            return resultado;
        }

        private async Task BuscarUsuariosInternoAsync(string termino, int limite, BusquedaGlobalResultado resultado)
        {
            var usuarios = await _context.Users
                .Where(u => u.UserName!.Contains(termino) ||
                           u.Email!.Contains(termino) ||
                           (u.NombreCompleto != null && u.NombreCompleto.Contains(termino)) ||
                           (u.Seudonimo != null && u.Seudonimo.Contains(termino)) ||
                           u.Id == termino)
                .Take(limite)
                .Select(u => new ResultadoBusqueda
                {
                    Id = u.Id,
                    Tipo = "Usuario",
                    Titulo = u.NombreCompleto ?? u.UserName ?? "",
                    Subtitulo = u.Email ?? "",
                    Icono = "bi-person",
                    Url = $"/Admin/Usuarios?buscar={u.Id}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Username", u.UserName ?? "" },
                        { "Email", u.Email ?? "" },
                        { "Creador", u.EsCreador ? "Sí" : "No" },
                        { "Verificado", u.CreadorVerificado ? "Sí" : "No" }
                    }
                })
                .ToListAsync();

            resultado.Usuarios = usuarios;
        }

        private async Task BuscarContenidosInternoAsync(string termino, int limite, BusquedaGlobalResultado resultado)
        {
            // Buscar por ID si es numérico
            int.TryParse(termino, out int idContenido);

            var contenidos = await _context.Contenidos
                .Include(c => c.Usuario)
                .Where(c => c.Id == idContenido ||
                           (c.Descripcion != null && c.Descripcion.Contains(termino)))
                .Take(limite)
                .Select(c => new ResultadoBusqueda
                {
                    Id = c.Id.ToString(),
                    Tipo = "Contenido",
                    Titulo = c.Descripcion != null ? (c.Descripcion.Length > 50 ? c.Descripcion.Substring(0, 50) + "..." : c.Descripcion) : $"Contenido #{c.Id}",
                    Subtitulo = c.Usuario != null ? $"por {c.Usuario.NombreCompleto ?? c.Usuario.UserName}" : "",
                    Icono = c.TipoContenido == TipoContenido.Video ? "bi-camera-video" : "bi-image",
                    Url = $"/Admin/Contenido?id={c.Id}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Tipo", c.TipoContenido.ToString() },
                        { "Fecha", c.FechaPublicacion.ToString("dd/MM/yyyy") },
                        { "Likes", c.NumeroLikes.ToString() }
                    }
                })
                .ToListAsync();

            resultado.Contenidos = contenidos;
        }

        private async Task BuscarReportesInternoAsync(string termino, int limite, BusquedaGlobalResultado resultado)
        {
            int.TryParse(termino, out int idReporte);

            var reportes = await _context.Reportes
                .Include(r => r.UsuarioReportador)
                .Where(r => r.Id == idReporte ||
                           (r.Motivo != null && r.Motivo.Contains(termino)) ||
                           (r.Descripcion != null && r.Descripcion.Contains(termino)))
                .Take(limite)
                .Select(r => new ResultadoBusqueda
                {
                    Id = r.Id.ToString(),
                    Tipo = "Reporte",
                    Titulo = $"Reporte #{r.Id} - {r.Motivo}",
                    Subtitulo = $"Estado: {r.Estado}",
                    Icono = "bi-flag",
                    Url = $"/Admin/Reportes?id={r.Id}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Estado", r.Estado ?? "" },
                        { "Tipo", r.TipoReporte ?? "" },
                        { "Fecha", r.FechaReporte.ToString("dd/MM/yyyy") }
                    }
                })
                .ToListAsync();

            resultado.Reportes = reportes;
        }

        private async Task BuscarApelacionesInternoAsync(string termino, int limite, BusquedaGlobalResultado resultado)
        {
            int.TryParse(termino, out int idApelacion);

            var apelaciones = await _context.Apelaciones
                .Include(a => a.Usuario)
                .Where(a => a.Id == idApelacion ||
                           a.Argumentos.Contains(termino))
                .Take(limite)
                .Select(a => new ResultadoBusqueda
                {
                    Id = a.Id.ToString(),
                    Tipo = "Apelación",
                    Titulo = $"Apelación #{a.Id}",
                    Subtitulo = $"Usuario: {(a.Usuario != null ? a.Usuario.UserName : "N/A")} - {a.Estado}",
                    Icono = "bi-chat-left-text",
                    Url = $"/Admin/Apelaciones?id={a.Id}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Estado", a.Estado.ToString() },
                        { "Fecha", a.FechaCreacion.ToString("dd/MM/yyyy") }
                    }
                })
                .ToListAsync();

            resultado.Apelaciones = apelaciones;
        }

        private async Task BuscarTransaccionesInternoAsync(string termino, int limite, BusquedaGlobalResultado resultado)
        {
            int.TryParse(termino, out int idTransaccion);

            var transacciones = await _context.Transacciones
                .Include(t => t.Usuario)
                .Where(t => t.Id == idTransaccion ||
                           (t.Usuario != null && (t.Usuario.UserName!.Contains(termino) || t.Usuario.Email!.Contains(termino))))
                .Take(limite)
                .Select(t => new ResultadoBusqueda
                {
                    Id = t.Id.ToString(),
                    Tipo = "Transacción",
                    Titulo = $"#{t.Id} - {t.TipoTransaccion} ${t.Monto:N2}",
                    Subtitulo = $"{(t.Usuario != null ? t.Usuario.UserName : "N/A")} - {t.EstadoTransaccion}",
                    Icono = "bi-cash-stack",
                    Url = $"/Admin/Transacciones?id={t.Id}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Monto", $"${t.Monto:N2}" },
                        { "Tipo", t.TipoTransaccion.ToString() },
                        { "Estado", t.EstadoTransaccion.ToString() }
                    }
                })
                .ToListAsync();

            resultado.Transacciones = transacciones;
        }

        public async Task<List<ResultadoBusqueda>> BuscarUsuariosAsync(string termino, int limite = 20)
        {
            var resultado = new BusquedaGlobalResultado();
            await BuscarUsuariosInternoAsync(termino, limite, resultado);
            return resultado.Usuarios;
        }

        public async Task<List<ResultadoBusqueda>> BuscarContenidosAsync(string termino, int limite = 20)
        {
            var resultado = new BusquedaGlobalResultado();
            await BuscarContenidosInternoAsync(termino, limite, resultado);
            return resultado.Contenidos;
        }

        public async Task<List<ResultadoBusqueda>> BuscarReportesAsync(string termino, int limite = 20)
        {
            var resultado = new BusquedaGlobalResultado();
            await BuscarReportesInternoAsync(termino, limite, resultado);
            return resultado.Reportes;
        }
    }

    public class BusquedaGlobalResultado
    {
        public List<ResultadoBusqueda> Usuarios { get; set; } = new();
        public List<ResultadoBusqueda> Contenidos { get; set; } = new();
        public List<ResultadoBusqueda> Reportes { get; set; } = new();
        public List<ResultadoBusqueda> Apelaciones { get; set; } = new();
        public List<ResultadoBusqueda> Transacciones { get; set; } = new();

        public int TotalResultados =>
            Usuarios.Count + Contenidos.Count + Reportes.Count + Apelaciones.Count + Transacciones.Count;
    }

    public class ResultadoBusqueda
    {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Subtitulo { get; set; } = "";
        public string Icono { get; set; } = "bi-search";
        public string Url { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
