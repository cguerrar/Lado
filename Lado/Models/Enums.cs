namespace Lado.Models
{
    public enum TipoUsuario
    {
        Fan = 0,
        Creador = 1,
        Agencia = 2
    }

    public enum TipoReaccion
    {
        Like = 0,
        Fire = 1,      // 🔥
        Heart = 2,     // ❤️
        Wow = 3,       // 😍
        Clap = 4       // 👏
    }

    public enum EstadoTransaccion
    {
        Pendiente = 0,
        Procesando = 1,
        Completada = 2,
        Fallida = 3,
        Cancelada = 4,
        Reembolsada = 5
    }

    public enum TipoContenido
    {
        Post = 0,
        Foto = 1,
        Imagen = 1,
        Video = 2,
        Audio = 3
    }

    public enum VisibilidadContenido
    {
        Privado = 0,
        Publico = 1
    }

    public enum TipoTransaccion
    {
        // Ingresos del usuario (Fan)
        Recarga = 0,
        Reembolso = 1,
        Bonificacion = 2, // ← AGREGADO

        // Gastos del usuario (Fan)
        Suscripcion = 10,
        CompraContenido = 11,
        Propina = 12,
        Tip = 12, // ← AGREGADO (alias de Propina)

        // Ingresos del creador
        IngresoSuscripcion = 20,
        VentaContenido = 21,
        IngresoPropina = 22,

        // Retiros
        Retiro = 30,
        RetiroPendiente = 31,
        RetiroCompletado = 32,

        // Otros
        Comision = 40,
        Ajuste = 41
    }

    public enum TipoLado
    {
        LadoA = 0,  // Público, gratuito, perfil real
        LadoB = 1   // Monetizado, seudónimo
    }



}