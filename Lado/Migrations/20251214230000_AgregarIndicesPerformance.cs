using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndicesPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Índice compuesto para consultas de transacciones por usuario y fecha
            // Usado en: DashboardController, BilleteraController
            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_UsuarioId_FechaTransaccion",
                table: "Transacciones",
                columns: new[] { "UsuarioId", "FechaTransaccion" });

            // Índice compuesto para transacciones por tipo y estado
            // Usado en: Reportes de ingresos, Dashboard
            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_TipoTransaccion_EstadoPago",
                table: "Transacciones",
                columns: new[] { "TipoTransaccion", "EstadoPago" });

            // Índice para contenidos por fecha de publicación (ordenamiento más común)
            // Usado en: Feed, Explorar, Perfil
            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_FechaPublicacion",
                table: "Contenidos",
                column: "FechaPublicacion");

            // Índice compuesto para contenidos por tipo de lado y estado
            // Usado en: Filtros de feed, búsqueda
            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_TipoLado_Censurado_FechaPublicacion",
                table: "Contenidos",
                columns: new[] { "TipoLado", "Censurado", "FechaPublicacion" });

            // Índice para suscripciones activas por creador
            // Usado en: Conteo de suscriptores, Dashboard de creador
            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_CreadorId_EstaActiva",
                table: "Suscripciones",
                columns: new[] { "CreadorId", "EstaActiva" });

            // Índice para suscripciones por fan activas
            // Usado en: Dashboard de fan, verificación de acceso
            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_FanId_EstaActiva",
                table: "Suscripciones",
                columns: new[] { "FanId", "EstaActiva" });

            // Índice para próximas renovaciones (jobs de renovación)
            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_ProximaRenovacion",
                table: "Suscripciones",
                column: "ProximaRenovacion",
                filter: "[EstaActiva] = 1");

            // Índice para likes por contenido (conteo rápido)
            migrationBuilder.CreateIndex(
                name: "IX_Likes_ContenidoId",
                table: "Likes",
                column: "ContenidoId");

            // Índice para mensajes no leídos
            // Usado en: Contador de notificaciones, bandeja de entrada
            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_DestinatarioId_Leido",
                table: "MensajesPrivados",
                columns: new[] { "DestinatarioId", "Leido" });

            // Índice para búsqueda de usuarios activos
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EstaActivo_FechaRegistro",
                table: "AspNetUsers",
                columns: new[] { "EstaActivo", "FechaRegistro" });

            // Índice para pistas musicales activas y trending
            migrationBuilder.CreateIndex(
                name: "IX_PistasMusica_Activo_ContadorUsos",
                table: "PistasMusica",
                columns: new[] { "Activo", "ContadorUsos" });

            // Índice para stories activas (no expiradas)
            migrationBuilder.CreateIndex(
                name: "IX_Stories_FechaExpiracion",
                table: "Stories",
                column: "FechaExpiracion");

            // Índice para anuncios activos por presupuesto
            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_Activo_PresupuestoDiario",
                table: "Anuncios",
                columns: new[] { "Activo", "PresupuestoDiario" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transacciones_UsuarioId_FechaTransaccion",
                table: "Transacciones");

            migrationBuilder.DropIndex(
                name: "IX_Transacciones_TipoTransaccion_EstadoPago",
                table: "Transacciones");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_FechaPublicacion",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_TipoLado_Censurado_FechaPublicacion",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Suscripciones_CreadorId_EstaActiva",
                table: "Suscripciones");

            migrationBuilder.DropIndex(
                name: "IX_Suscripciones_FanId_EstaActiva",
                table: "Suscripciones");

            migrationBuilder.DropIndex(
                name: "IX_Suscripciones_ProximaRenovacion",
                table: "Suscripciones");

            migrationBuilder.DropIndex(
                name: "IX_Likes_ContenidoId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_MensajesPrivados_DestinatarioId_Leido",
                table: "MensajesPrivados");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_EstaActivo_FechaRegistro",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_PistasMusica_Activo_ContadorUsos",
                table: "PistasMusica");

            migrationBuilder.DropIndex(
                name: "IX_Stories_FechaExpiracion",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Anuncios_Activo_PresupuestoDiario",
                table: "Anuncios");
        }
    }
}
