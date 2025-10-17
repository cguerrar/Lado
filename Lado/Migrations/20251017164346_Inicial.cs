using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Biografia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FotoPerfil = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FotoPortada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipoUsuario = table.Column<int>(type: "int", nullable: false),
                    EsCreador = table.Column<bool>(type: "bit", nullable: false),
                    PrecioSuscripcion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NumeroSeguidores = table.Column<int>(type: "int", nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalGanancias = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    EsVerificado = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Pais = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AgeVerified = table.Column<bool>(type: "bit", nullable: false),
                    AgeVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadorVerificado = table.Column<bool>(type: "bit", nullable: false),
                    FechaVerificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgeVerificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaVerificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EdadAlVerificar = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgeVerificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgeVerificationLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMensajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemitenteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DestinatarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leido = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoPorRemitente = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoPorDestinatario = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMensajes_AspNetUsers_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMensajes_AspNetUsers_RemitenteId",
                        column: x => x.RemitenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contenidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoContenido = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RutaArchivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Thumbnail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EsPremium = table.Column<bool>(type: "bit", nullable: false),
                    PrecioDesbloqueo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EsBorrador = table.Column<bool>(type: "bit", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Censurado = table.Column<bool>(type: "bit", nullable: false),
                    RazonCensura = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeroLikes = table.Column<int>(type: "int", nullable: false),
                    NumeroComentarios = table.Column<int>(type: "int", nullable: false),
                    NumeroVistas = table.Column<int>(type: "int", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contenidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contenidos_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatorVerificationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentoIdentidadPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfieConDocumentoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PruebaDireccionPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevisadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorVerificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorVerificationRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Desafios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FanId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreadorObjetivoId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreadorAsignadoId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TipoDesafio = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Presupuesto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioFinal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiasPlazoPlazo = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoContenido = table.Column<int>(type: "int", nullable: false),
                    Visibilidad = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCompletado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstadoPago = table.Column<int>(type: "int", nullable: false),
                    RutaContenido = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NotasCreador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    ComentarioRating = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivoEntregaUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEntrega = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Desafios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Desafios_AspNetUsers_CreadorAsignadoId",
                        column: x => x.CreadorAsignadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafios_AspNetUsers_CreadorObjetivoId",
                        column: x => x.CreadorObjetivoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafios_AspNetUsers_FanId",
                        column: x => x.FanId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MensajesPrivados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemitenteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DestinatarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leido = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoPorRemitente = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoPorDestinatario = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesPrivados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajesPrivados_AspNetUsers_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MensajesPrivados_AspNetUsers_RemitenteId",
                        column: x => x.RemitenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suscripciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FanId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrecioMensual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCancelacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximaRenovacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RenovacionAutomatica = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suscripciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suscripciones_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Suscripciones_AspNetUsers_FanId",
                        column: x => x.FanId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FanId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tips_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tips_AspNetUsers_FanId",
                        column: x => x.FanId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transacciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoTransaccion = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoNeto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comision = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaTransaccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstadoPago = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstadoTransaccion = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MetodoPago = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transacciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transacciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comentarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comentarios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comentarios_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComprasContenido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaCompra = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasContenido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasContenido_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComprasContenido_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    FechaLike = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Likes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Likes_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reportes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioReportadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UsuarioReportadoId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ContenidoReportadoId = table.Column<int>(type: "int", nullable: true),
                    ContenidoId = table.Column<int>(type: "int", nullable: true),
                    TipoReporte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaReporte = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reportes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reportes_AspNetUsers_UsuarioReportadoId",
                        column: x => x.UsuarioReportadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reportes_AspNetUsers_UsuarioReportadorId",
                        column: x => x.UsuarioReportadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reportes_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reportes_Contenidos_ContenidoReportadoId",
                        column: x => x.ContenidoReportadoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PropuestasDesafios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesafioId = table.Column<int>(type: "int", nullable: false),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrecioPropuesto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiasEntrega = table.Column<int>(type: "int", nullable: false),
                    MensajePropuesta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UrlsPortfolio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaPropuesta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropuestasDesafios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropuestasDesafios_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropuestasDesafios_Desafios_DesafioId",
                        column: x => x.DesafioId,
                        principalTable: "Desafios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgeVerificationLogs_UserId",
                table: "AgeVerificationLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EsCreador",
                table: "AspNetUsers",
                column: "EsCreador");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TipoUsuario",
                table: "AspNetUsers",
                column: "TipoUsuario");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMensajes_DestinatarioId",
                table: "ChatMensajes",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMensajes_FechaEnvio",
                table: "ChatMensajes",
                column: "FechaEnvio");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMensajes_RemitenteId",
                table: "ChatMensajes",
                column: "RemitenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Comentarios_ContenidoId",
                table: "Comentarios",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Comentarios_FechaCreacion",
                table: "Comentarios",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_Comentarios_UsuarioId",
                table: "Comentarios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasContenido_ContenidoId",
                table: "ComprasContenido",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasContenido_UsuarioId_ContenidoId",
                table: "ComprasContenido",
                columns: new[] { "UsuarioId", "ContenidoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_EstaActivo",
                table: "Contenidos",
                column: "EstaActivo");

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_FechaPublicacion",
                table: "Contenidos",
                column: "FechaPublicacion");

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_UsuarioId",
                table: "Contenidos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorVerificationRequests_UserId",
                table: "CreatorVerificationRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_Categoria",
                table: "Desafios",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_CreadorAsignadoId",
                table: "Desafios",
                column: "CreadorAsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_CreadorObjetivoId",
                table: "Desafios",
                column: "CreadorObjetivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_Estado",
                table: "Desafios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_FanId",
                table: "Desafios",
                column: "FanId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafios_FechaCreacion",
                table: "Desafios",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_ContenidoId",
                table: "Likes",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_Usuario_Contenido_Unique",
                table: "Likes",
                columns: new[] { "UsuarioId", "ContenidoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_DestinatarioId",
                table: "MensajesPrivados",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_FechaEnvio",
                table: "MensajesPrivados",
                column: "FechaEnvio");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_Leido",
                table: "MensajesPrivados",
                column: "Leido");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_RemitenteId",
                table: "MensajesPrivados",
                column: "RemitenteId");

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasDesafios_CreadorId",
                table: "PropuestasDesafios",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasDesafios_DesafioId",
                table: "PropuestasDesafios",
                column: "DesafioId");

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasDesafios_Estado",
                table: "PropuestasDesafios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasDesafios_FechaPropuesta",
                table: "PropuestasDesafios",
                column: "FechaPropuesta");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_ContenidoId",
                table: "Reportes",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_ContenidoReportadoId",
                table: "Reportes",
                column: "ContenidoReportadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_FechaReporte",
                table: "Reportes",
                column: "FechaReporte");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_UsuarioReportadoId",
                table: "Reportes",
                column: "UsuarioReportadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_UsuarioReportadorId",
                table: "Reportes",
                column: "UsuarioReportadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_CreadorId",
                table: "Suscripciones",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_EstaActiva",
                table: "Suscripciones",
                column: "EstaActiva");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_Fan_Creador_Activa",
                table: "Suscripciones",
                columns: new[] { "FanId", "CreadorId", "EstaActiva" });

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_FanId",
                table: "Suscripciones",
                column: "FanId");

            migrationBuilder.CreateIndex(
                name: "IX_Tips_CreadorId",
                table: "Tips",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tips_FanId",
                table: "Tips",
                column: "FanId");

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_EstadoTransaccion",
                table: "Transacciones",
                column: "EstadoTransaccion");

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_FechaTransaccion",
                table: "Transacciones",
                column: "FechaTransaccion");

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_UsuarioId",
                table: "Transacciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgeVerificationLogs");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChatMensajes");

            migrationBuilder.DropTable(
                name: "Comentarios");

            migrationBuilder.DropTable(
                name: "ComprasContenido");

            migrationBuilder.DropTable(
                name: "CreatorVerificationRequests");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "MensajesPrivados");

            migrationBuilder.DropTable(
                name: "PropuestasDesafios");

            migrationBuilder.DropTable(
                name: "Reportes");

            migrationBuilder.DropTable(
                name: "Suscripciones");

            migrationBuilder.DropTable(
                name: "Tips");

            migrationBuilder.DropTable(
                name: "Transacciones");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Desafios");

            migrationBuilder.DropTable(
                name: "Contenidos");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
