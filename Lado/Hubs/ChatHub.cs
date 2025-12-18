using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Lado.Hubs
{
    /// <summary>
    /// Hub de SignalR para mensajería en tiempo real tipo WhatsApp
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Se ejecuta cuando un usuario se conecta al hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Agregar usuario a su grupo personal para recibir mensajes
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                _logger.LogInformation("Usuario {UserId} conectado al chat hub", userId);
            }
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Se ejecuta cuando un usuario se desconecta del hub
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                _logger.LogInformation("Usuario {UserId} desconectado del chat hub", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Unirse a una conversación específica (para notificaciones de escritura)
        /// </summary>
        public async Task JoinConversation(string otroUsuarioId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            // Crear un ID de conversación único (ordenado alfabéticamente)
            var conversationId = GetConversationId(userId, otroUsuarioId);
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
            _logger.LogDebug("Usuario {UserId} se unió a conversación {ConversationId}", userId, conversationId);
        }

        /// <summary>
        /// Salir de una conversación
        /// </summary>
        public async Task LeaveConversation(string otroUsuarioId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            var conversationId = GetConversationId(userId, otroUsuarioId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
        }

        /// <summary>
        /// Notificar que el usuario está escribiendo
        /// </summary>
        public async Task SendTyping(string destinatarioId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            if (string.IsNullOrEmpty(userId)) return;

            // Notificar al destinatario que el usuario está escribiendo
            await Clients.Group($"user_{destinatarioId}").SendAsync("UserTyping", new
            {
                UserId = userId,
                UserName = userName
            });
        }

        /// <summary>
        /// Notificar que el usuario dejó de escribir
        /// </summary>
        public async Task StopTyping(string destinatarioId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.Group($"user_{destinatarioId}").SendAsync("UserStoppedTyping", new
            {
                UserId = userId
            });
        }

        /// <summary>
        /// Marcar mensajes como leídos y notificar al remitente
        /// </summary>
        public async Task MarkAsRead(string remitenteId, int[] mensajeIds)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            // Notificar al remitente que sus mensajes fueron leídos
            await Clients.Group($"user_{remitenteId}").SendAsync("MensajesLeidos", new
            {
                LectorId = userId,
                MensajeIds = mensajeIds,
                FechaLectura = DateTime.Now
            });
        }

        /// <summary>
        /// Genera un ID único para la conversación entre dos usuarios
        /// </summary>
        private string GetConversationId(string userId1, string userId2)
        {
            // Ordenar alfabéticamente para que siempre sea el mismo ID
            var ids = new[] { userId1, userId2 }.OrderBy(x => x).ToArray();
            return $"conv_{ids[0]}_{ids[1]}";
        }
    }
}
