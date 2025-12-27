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

    public enum TipoCensuraPreview
    {
        Blur = 0,       // Desenfoque gaussiano
        Pixelado = 1,   // Pixelado estilo mosaico
        Silueta = 2,    // Solo silueta/sombra
        Parcial = 3     // Solo 10-20% visible
    }

    // ====================================
    // ENUMS PARA SISTEMA DE AGENCIAS
    // ====================================

    public enum EstadoAgencia
    {
        Pendiente = 0,
        Activa = 1,
        Suspendida = 2,
        Rechazada = 3,
        Cancelada = 4
    }

    public enum EstadoAnuncio
    {
        Borrador = 0,
        EnRevision = 1,
        Activo = 2,
        Pausado = 3,
        Finalizado = 4,
        Rechazado = 5
    }

    public enum TipoCreativo
    {
        Imagen = 0,
        Video = 1,
        Carousel = 2
    }

    public enum TipoTransaccionAgencia
    {
        RecargaSaldo = 0,
        CobroCPM = 1,
        CobroCPC = 2,
        Reembolso = 3,
        Ajuste = 4
    }

    // ====================================
    // ENUMS PARA SISTEMA DE INTERESES
    // ====================================

    public enum TipoInteres
    {
        Explicito = 0,  // Seleccionado por el usuario
        Implicito = 1   // Inferido por comportamiento
    }

    public enum GeneroUsuario
    {
        NoEspecificado = 0,
        Masculino = 1,
        Femenino = 2,
        Otro = 3
    }

    public enum TipoInteraccion
    {
        Vista = 0,
        Like = 1,
        Comentario = 2,
        Compartir = 3,
        Guardar = 4,
        VistaCompleta = 5,
        ClicPerfil = 6
    }

    public enum TextoBotonAnuncio
    {
        VerMas = 0,
        Suscribirse = 1,
        Comprar = 2,
        Descargar = 3,
        Registrarse = 4,
        MasInformacion = 5
    }

    // ====================================
    // ENUMS PARA SISTEMA DE MENSAJES
    // ====================================

    public enum TipoMensaje
    {
        Texto = 0,
        Imagen = 1,
        Video = 2
    }

    // ====================================
    // ENUMS PARA RESPUESTAS A STORIES
    // ====================================

    public enum TipoRespuestaStory
    {
        Texto = 0,          // Mensaje de texto normal
        ReaccionFuego = 1,  // 🔥
        ReaccionCorazon = 2,// ❤️
        ReaccionRisa = 3,   // 😂
        ReaccionSorpresa = 4,// 😮
        ReaccionAplauso = 5 // 👏
    }
}