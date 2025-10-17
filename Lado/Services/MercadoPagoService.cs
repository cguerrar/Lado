namespace Lado.Services
{
    // Servicio de Mercado Pago - Versión Simplificada para MVP
    // Los pagos están simulados. Para activar pagos reales, instalar el SDK de MercadoPago
    public class MercadoPagoService
    {
        private readonly IConfiguration _configuration;

        public MercadoPagoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Simula la creación de una preferencia de pago
        public async Task<PreferenciaSimulada> CrearPreferenciaSuscripcion(
            string titulo,
            decimal precio,
            string emailPagador,
            string referencia)
        {
            // Por ahora solo simula el pago
            // Para activar Mercado Pago real, ver instrucciones en README.md
            await Task.Delay(100); // Simula llamada a API

            return new PreferenciaSimulada
            {
                Id = Guid.NewGuid().ToString(),
                InitPoint = "#", // En producción sería el link de pago
                SandboxInitPoint = "#",
                Titulo = titulo,
                Precio = precio,
                EmailPagador = emailPagador,
                Referencia = referencia
            };
        }

        public async Task<PreferenciaSimulada> CrearPreferenciaTip(
            string nombreCreador,
            decimal monto,
            string emailPagador,
            string referencia)
        {
            return await CrearPreferenciaSuscripcion(
                $"Tip para {nombreCreador}",
                monto,
                emailPagador,
                referencia
            );
        }

        // Verifica el estado de un pago (simulado)
        public async Task<bool> VerificarPago(string paymentId)
        {
            await Task.Delay(50);
            return true; // Simula pago exitoso
        }
    }

    // Clase auxiliar para simular respuesta de Mercado Pago
    public class PreferenciaSimulada
    {
        public string Id { get; set; }
        public string InitPoint { get; set; }
        public string SandboxInitPoint { get; set; }
        public string Titulo { get; set; }
        public decimal Precio { get; set; }
        public string EmailPagador { get; set; }
        public string Referencia { get; set; }
    }
}