using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class SeguridadAvanzada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo agregar SecurityVersion si no existe
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'SecurityVersion')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [SecurityVersion] int NOT NULL DEFAULT 0
                END
            ");

            // Solo crear ActiveTokens si no existe
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ActiveTokens')
                BEGIN
                    CREATE TABLE [ActiveTokens] (
                        [Id] int NOT NULL IDENTITY,
                        [Jti] nvarchar(100) NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [ExpiresAt] datetime2 NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [IsRevoked] bit NOT NULL,
                        [DeviceInfo] nvarchar(500) NULL,
                        [IpAddress] nvarchar(50) NULL,
                        CONSTRAINT [PK_ActiveTokens] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ActiveTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_ActiveTokens_Jti] ON [ActiveTokens] ([Jti]);
                    CREATE INDEX [IX_ActiveTokens_UserId] ON [ActiveTokens] ([UserId]);
                    CREATE INDEX [IX_ActiveTokens_Cleanup] ON [ActiveTokens] ([ExpiresAt], [IsRevoked]);
                END
            ");

            // Solo crear IntentosAtaque si no existe
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IntentosAtaque')
                BEGIN
                    CREATE TABLE [IntentosAtaque] (
                        [Id] int NOT NULL IDENTITY,
                        [DireccionIp] nvarchar(45) NOT NULL,
                        [Fecha] datetime2 NOT NULL,
                        [TipoAtaque] int NOT NULL,
                        [Endpoint] nvarchar(200) NULL,
                        [UsuarioId] nvarchar(100) NULL,
                        [UserAgent] nvarchar(100) NULL,
                        [ResultoEnBloqueo] bit NOT NULL,
                        CONSTRAINT [PK_IntentosAtaque] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Solo crear IpsBloqueadas si no existe
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IpsBloqueadas')
                BEGIN
                    CREATE TABLE [IpsBloqueadas] (
                        [Id] int NOT NULL IDENTITY,
                        [DireccionIp] nvarchar(45) NOT NULL,
                        [Razon] nvarchar(500) NULL,
                        [FechaBloqueo] datetime2 NOT NULL,
                        [FechaExpiracion] datetime2 NULL,
                        [AdminId] nvarchar(max) NULL,
                        [EstaActivo] bit NOT NULL,
                        [IntentosBloqueos] int NOT NULL,
                        [UltimoIntento] datetime2 NULL,
                        [TipoBloqueo] int NOT NULL,
                        [TipoAtaque] int NOT NULL,
                        [ViolacionesRateLimit] int NOT NULL,
                        CONSTRAINT [PK_IpsBloqueadas] PRIMARY KEY ([Id])
                    )
                END
            ");

            // Solo crear RefreshTokens si no existe
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens')
                BEGIN
                    CREATE TABLE [RefreshTokens] (
                        [Id] int NOT NULL IDENTITY,
                        [Token] nvarchar(500) NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [ExpiryDate] datetime2 NOT NULL,
                        [IsRevoked] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [DeviceInfo] nvarchar(500) NULL,
                        [IpAddress] nvarchar(50) NULL,
                        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
                    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
                    CREATE INDEX [IX_RefreshTokens_User_Active] ON [RefreshTokens] ([UserId], [IsRevoked], [ExpiryDate]);
                END
            ");

            /* Código original comentado - las tablas se crean con SQL condicional arriba
            migrationBuilder.CreateTable(
                name: "ActiveTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Jti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    DeviceInfo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntentosAtaque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DireccionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoAtaque = table.Column<int>(type: "int", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsuarioId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResultoEnBloqueo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosAtaque", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IpsBloqueadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DireccionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Razon = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaBloqueo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    IntentosBloqueos = table.Column<int>(type: "int", nullable: false),
                    UltimoIntento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TipoBloqueo = table.Column<int>(type: "int", nullable: false),
                    TipoAtaque = table.Column<int>(type: "int", nullable: false),
                    ViolacionesRateLimit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpsBloqueadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceInfo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 16, 29, 1, 939, DateTimeKind.Local).AddTicks(2749));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 16, 29, 1, 939, DateTimeKind.Local).AddTicks(2848));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 16, 29, 1, 939, DateTimeKind.Local).AddTicks(2850));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 16, 29, 1, 939, DateTimeKind.Local).AddTicks(2851));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 16, 29, 1, 939, DateTimeKind.Local).AddTicks(2852));

            migrationBuilder.CreateIndex(
                name: "IX_ActiveTokens_Cleanup",
                table: "ActiveTokens",
                columns: new[] { "ExpiresAt", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveTokens_Jti",
                table: "ActiveTokens",
                column: "Jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveTokens_UserId",
                table: "ActiveTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_User_Active",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveTokens");

            migrationBuilder.DropTable(
                name: "IntentosAtaque");

            migrationBuilder.DropTable(
                name: "IpsBloqueadas");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 18, 22, 58, 40, 991, DateTimeKind.Local).AddTicks(846));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 18, 22, 58, 40, 991, DateTimeKind.Local).AddTicks(907));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 18, 22, 58, 40, 991, DateTimeKind.Local).AddTicks(910));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 18, 22, 58, 40, 991, DateTimeKind.Local).AddTicks(911));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 18, 22, 58, 40, 991, DateTimeKind.Local).AddTicks(912));
        }
    }
}
