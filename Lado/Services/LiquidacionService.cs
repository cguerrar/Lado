using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Lado.Models;

namespace Lado.Services
{
    /// <summary>
    /// Interfaz para el servicio de generación de liquidaciones PDF
    /// </summary>
    public interface ILiquidacionService
    {
        /// <summary>
        /// Genera un PDF de liquidación para un retiro
        /// </summary>
        Task<string> GenerarLiquidacionPdfAsync(Transaccion transaccion, ApplicationUser usuario);

        /// <summary>
        /// Obtiene los bytes del PDF de liquidación
        /// </summary>
        Task<byte[]> GenerarLiquidacionBytesAsync(Transaccion transaccion, ApplicationUser usuario);
    }

    /// <summary>
    /// Servicio para generar documentos de liquidación en PDF
    /// </summary>
    public class LiquidacionService : ILiquidacionService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LiquidacionService> _logger;

        public LiquidacionService(
            IWebHostEnvironment environment,
            ILogger<LiquidacionService> logger)
        {
            _environment = environment;
            _logger = logger;

            // Configurar licencia de QuestPDF (Community es gratis)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerarLiquidacionPdfAsync(Transaccion transaccion, ApplicationUser usuario)
        {
            try
            {
                // Crear directorio si no existe
                var liquidacionesDir = Path.Combine(_environment.WebRootPath, "liquidaciones");
                if (!Directory.Exists(liquidacionesDir))
                {
                    Directory.CreateDirectory(liquidacionesDir);
                }

                // Generar nombre único para el archivo
                var nombreArchivo = $"liquidacion_{transaccion.Id}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                var rutaCompleta = Path.Combine(liquidacionesDir, nombreArchivo);

                // Generar el PDF
                var documento = CrearDocumentoPdf(transaccion, usuario);
                documento.GeneratePdf(rutaCompleta);

                _logger.LogInformation("Liquidación PDF generada: {Ruta}", rutaCompleta);

                // Retornar la ruta relativa para acceso web
                return $"/liquidaciones/{nombreArchivo}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar liquidación PDF para transacción {TransaccionId}", transaccion.Id);
                throw;
            }
        }

        public async Task<byte[]> GenerarLiquidacionBytesAsync(Transaccion transaccion, ApplicationUser usuario)
        {
            try
            {
                var documento = CrearDocumentoPdf(transaccion, usuario);
                return documento.GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar bytes de liquidación para transacción {TransaccionId}", transaccion.Id);
                throw;
            }
        }

        private Document CrearDocumentoPdf(Transaccion transaccion, ApplicationUser usuario)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(header => CrearEncabezado(header, transaccion));
                    page.Content().Element(content => CrearContenido(content, transaccion, usuario));
                    page.Footer().Element(footer => CrearPiePagina(footer, transaccion));
                });
            });
        }

        private void CrearEncabezado(IContainer container, Transaccion transaccion)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("LADO")
                        .FontSize(28)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    col.Item().Text("www.ladoapp.com")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Medium);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("LIQUIDACIÓN DE RETIRO")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    col.Item().Text($"Nº {transaccion.Id:D8}")
                        .FontSize(12)
                        .FontColor(Colors.Grey.Darken1);

                    col.Item().Text($"Fecha: {transaccion.FechaTransaccion:dd/MM/yyyy HH:mm}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Medium);
                });
            });
        }

        private void CrearContenido(IContainer container, Transaccion transaccion, ApplicationUser usuario)
        {
            container.PaddingVertical(20).Column(col =>
            {
                // Línea separadora
                col.Item().PaddingBottom(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // Información del creador
                col.Item().PaddingBottom(15).Element(c => CrearSeccionCreador(c, usuario));

                // Detalles del retiro
                col.Item().PaddingBottom(15).Element(c => CrearSeccionDetalles(c, transaccion, usuario));

                // Desglose financiero
                col.Item().PaddingBottom(15).Element(c => CrearSeccionDesglose(c, transaccion));

                // Información de pago
                col.Item().PaddingBottom(15).Element(c => CrearSeccionPago(c, transaccion));

                // Notas
                if (!string.IsNullOrEmpty(transaccion.Notas))
                {
                    col.Item().Element(c => CrearSeccionNotas(c, transaccion));
                }

                // Aviso legal
                col.Item().PaddingTop(20).Element(CrearAvisoLegal);
            });
        }

        private void CrearSeccionCreador(IContainer container, ApplicationUser usuario)
        {
            container.Background(Colors.Grey.Lighten4).Padding(15).Column(col =>
            {
                col.Item().Text("DATOS DEL CREADOR")
                    .FontSize(11)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Nombre: {usuario.NombreCompleto ?? usuario.UserName}")
                            .FontSize(10);
                        c.Item().Text($"Usuario: @{usuario.UserName}")
                            .FontSize(10);
                        c.Item().Text($"Email: {usuario.Email}")
                            .FontSize(10);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"País: {usuario.Pais ?? "No especificado"}")
                            .FontSize(10);
                        c.Item().Text($"ID Usuario: {usuario.Id[..8]}...")
                            .FontSize(10);
                    });
                });
            });
        }

        private void CrearSeccionDetalles(IContainer container, Transaccion transaccion, ApplicationUser usuario)
        {
            container.Column(col =>
            {
                col.Item().Text("DETALLES DE LA OPERACIÓN")
                    .FontSize(11)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Cell().Padding(5).Text("Tipo de operación:").FontSize(10);
                    table.Cell().Padding(5).Text("Retiro de fondos").FontSize(10).Bold();

                    table.Cell().Padding(5).Text("Estado:").FontSize(10);
                    table.Cell().Padding(5).Text(transaccion.EstadoPago ?? "Pendiente")
                        .FontSize(10)
                        .Bold()
                        .FontColor(transaccion.EstadoPago == "Completado" ? Colors.Green.Darken1 : Colors.Orange.Darken1);

                    table.Cell().Padding(5).Text("Comisión aplicada:").FontSize(10);
                    table.Cell().Padding(5).Text($"{usuario.ComisionRetiro}%").FontSize(10);

                    table.Cell().Padding(5).Text("Método de pago:").FontSize(10);
                    table.Cell().Padding(5).Text(transaccion.MetodoPago ?? "No especificado").FontSize(10);
                });
            });
        }

        private void CrearSeccionDesglose(IContainer container, Transaccion transaccion)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
            {
                // Encabezado
                col.Item().Background(Colors.Blue.Darken2).Padding(10).Text("DESGLOSE FINANCIERO")
                    .FontSize(11)
                    .Bold()
                    .FontColor(Colors.White);

                col.Item().Padding(15).Column(inner =>
                {
                    // Monto bruto
                    inner.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Monto Bruto Solicitado:").FontSize(11);
                        row.ConstantItem(100).AlignRight().Text($"${transaccion.Monto:N2}")
                            .FontSize(11)
                            .Bold();
                    });

                    inner.Item().PaddingVertical(5).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    // Comisión LadoApp
                    inner.Item().Row(row =>
                    {
                        row.RelativeItem().Text("(-) Comisión LadoApp:").FontSize(10).FontColor(Colors.Red.Darken1);
                        row.ConstantItem(100).AlignRight().Text($"-${transaccion.Comision ?? 0:N2}")
                            .FontSize(10)
                            .FontColor(Colors.Red.Darken1);
                    });

                    // Comisión Billetera Electrónica
                    if (transaccion.ComisionBilleteraElectronica > 0)
                    {
                        inner.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("(-) Comisión Billetera Electrónica:").FontSize(10).FontColor(Colors.Purple.Darken1);
                            row.ConstantItem(100).AlignRight().Text($"-${transaccion.ComisionBilleteraElectronica ?? 0:N2}")
                                .FontSize(10)
                                .FontColor(Colors.Purple.Darken1);
                        });
                    }

                    // Retención de impuestos
                    if (transaccion.RetencionImpuestos > 0)
                    {
                        inner.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("(-) Retención de Impuestos:").FontSize(10).FontColor(Colors.Orange.Darken1);
                            row.ConstantItem(100).AlignRight().Text($"-${transaccion.RetencionImpuestos ?? 0:N2}")
                                .FontSize(10)
                                .FontColor(Colors.Orange.Darken1);
                        });
                    }

                    inner.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                    // Monto neto
                    inner.Item().Row(row =>
                    {
                        row.RelativeItem().Text("MONTO NETO A RECIBIR:").FontSize(12).Bold();
                        row.ConstantItem(100).AlignRight().Text($"${transaccion.MontoNeto ?? 0:N2}")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);
                    });
                });
            });
        }

        private void CrearSeccionPago(IContainer container, Transaccion transaccion)
        {
            container.Background(Colors.Green.Lighten5).Border(1).BorderColor(Colors.Green.Lighten2).Padding(15).Column(col =>
            {
                col.Item().Text("INFORMACIÓN DE PAGO")
                    .FontSize(11)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Método: {transaccion.MetodoPago ?? "No especificado"}")
                            .FontSize(10);
                        c.Item().Text($"Estado: {transaccion.EstadoPago ?? "Pendiente"}")
                            .FontSize(10);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Tiempo estimado de proceso:")
                            .FontSize(10);
                        c.Item().Text("3-5 días hábiles")
                            .FontSize(10)
                            .Bold();
                    });
                });
            });
        }

        private void CrearSeccionNotas(IContainer container, Transaccion transaccion)
        {
            container.Background(Colors.Yellow.Lighten4).Padding(10).Column(col =>
            {
                col.Item().Text("NOTAS ADICIONALES")
                    .FontSize(10)
                    .Bold()
                    .FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(5).Text(transaccion.Notas)
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        }

        private void CrearAvisoLegal(IContainer container)
        {
            container.Background(Colors.Grey.Lighten4).Padding(12).Column(col =>
            {
                col.Item().Text("AVISO IMPORTANTE")
                    .FontSize(9)
                    .Bold()
                    .FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(5).Text(
                    "Este documento es un comprobante de su solicitud de retiro. " +
                    "El monto neto indicado será transferido a su método de pago registrado una vez procesado. " +
                    "Las retenciones de impuestos se aplican según la legislación vigente en su país de residencia. " +
                    "Conserve este documento para sus registros fiscales.")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1)
                    .LineHeight(1.4f);
            });
        }

        private void CrearPiePagina(IContainer container, Transaccion transaccion)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("LADO - Plataforma de Creadores")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                        c.Item().Text("www.ladoapp.com | soporte@ladoapp.com")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text($"Documento generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                        c.Item().Text($"ID Transacción: {transaccion.Id}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }
    }
}
