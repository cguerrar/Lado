using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Lado.Hubs
{
    /// <summary>
    /// Hub de SignalR para moderación en tiempo real
    /// Permite a supervisores recibir notificaciones de nuevos items pendientes
    /// </summary>
    [Authorize(Roles = "Admin,Supervisor")]
    public class ModeracionHub : Hub
    {
        private readonly ILogger<ModeracionHub> _logger;
        private static readonly Dictionary<string, string> _connectedModerators = new();
        private static readonly object _lock = new();

        public ModeracionHub(ILogger<ModeracionHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Se ejecuta cuando un moderador se conecta
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lock)
                {
                    _connectedModerators[Context.ConnectionId] = userId;
                }

                // Unirse al grupo de moderadores
                await Groups.AddToGroupAsync(Context.ConnectionId, "moderadores");

                // Notificar a otros moderadores que alguien se conectó
                await Clients.OthersInGroup("moderadores").SendAsync("ModeradorConectado", new
                {
                    userId,
                    userName,
                    timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("[ModeracionHub] Moderador {UserName} ({UserId}) conectado", userName, userId);
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Se ejecuta cuando un moderador se desconecta
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lock)
                {
                    _connectedModerators.Remove(Context.ConnectionId);
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "moderadores");

                // Notificar a otros moderadores
                await Clients.OthersInGroup("moderadores").SendAsync("ModeradorDesconectado", new
                {
                    userId,
                    userName,
                    timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("[ModeracionHub] Moderador {UserName} ({UserId}) desconectado", userName, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Notifica que un moderador tomó un item
        /// </summary>
        public async Task TomarItem(int itemId, string tipoItem)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            await Clients.OthersInGroup("moderadores").SendAsync("ItemTomado", new
            {
                itemId,
                tipoItem,
                moderadorId = userId,
                moderadorNombre = userName,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Notifica que un moderador liberó un item
        /// </summary>
        public async Task LiberarItem(int itemId, string tipoItem)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            await Clients.OthersInGroup("moderadores").SendAsync("ItemLiberado", new
            {
                itemId,
                tipoItem,
                moderadorId = userId,
                moderadorNombre = userName,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Notifica que un item fue resuelto
        /// </summary>
        public async Task ItemResuelto(int itemId, string tipoItem, string accion)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            await Clients.Group("moderadores").SendAsync("ItemResuelto", new
            {
                itemId,
                tipoItem,
                accion,
                moderadorId = userId,
                moderadorNombre = userName,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Obtiene la lista de moderadores conectados
        /// </summary>
        public async Task ObtenerModeradoresConectados()
        {
            List<string> moderadores;
            lock (_lock)
            {
                moderadores = _connectedModerators.Values.Distinct().ToList();
            }

            await Clients.Caller.SendAsync("ModeradoresConectados", moderadores);
        }

        /// <summary>
        /// Método estático para obtener el conteo de moderadores conectados
        /// </summary>
        public static int ObtenerConteoModeradores()
        {
            lock (_lock)
            {
                return _connectedModerators.Values.Distinct().Count();
            }
        }

        /// <summary>
        /// Método estático para verificar si hay moderadores conectados
        /// </summary>
        public static bool HayModeradoresConectados()
        {
            lock (_lock)
            {
                return _connectedModerators.Count > 0;
            }
        }
    }
}
