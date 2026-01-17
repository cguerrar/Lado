namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Estados posibles de un item en la cola de moderación
    /// </summary>
    public enum EstadoModeracion
    {
        /// <summary>Pendiente de revisión, en cola</summary>
        Pendiente = 0,

        /// <summary>Asignado a un supervisor, en proceso de revisión</summary>
        EnRevision = 1,

        /// <summary>Aprobado por supervisor o IA</summary>
        Aprobado = 2,

        /// <summary>Rechazado - contenido no permitido</summary>
        Rechazado = 3,

        /// <summary>Censurado - visible pero con advertencia</summary>
        Censurado = 4,

        /// <summary>Escalado a administrador</summary>
        Escalado = 5,

        /// <summary>Auto-aprobado por IA con alta confianza</summary>
        AutoAprobado = 6,

        /// <summary>Auto-rechazado por IA con alta confianza</summary>
        AutoRechazado = 7
    }

    /// <summary>
    /// Prioridad de revisión en la cola
    /// </summary>
    public enum PrioridadModeracion
    {
        /// <summary>Baja - contenido clasificado como seguro por IA</summary>
        Baja = 3,

        /// <summary>Normal - contenido nuevo sin clasificar</summary>
        Normal = 2,

        /// <summary>Alta - contenido reportado o flaggeado por IA</summary>
        Alta = 1,

        /// <summary>Urgente - múltiples reportes o contenido peligroso</summary>
        Urgente = 0
    }

    /// <summary>
    /// Tipos de decisión que puede tomar un supervisor
    /// </summary>
    public enum TipoDecisionModeracion
    {
        Aprobado,
        Rechazado,
        Censurado,
        Escalado,
        Advertencia
    }

    /// <summary>
    /// Razones predefinidas para rechazo de contenido
    /// </summary>
    public enum RazonRechazo
    {
        ContenidoProhibido,
        MenorDeEdadAparente,
        ViolenciaExplicita,
        SpamPublicidad,
        InfraccionCopyright,
        CalidadMuyBaja,
        InformacionPersonalExpuesta,
        IncitacionOdio,
        ContenidoIlegal,
        Otro
    }

    /// <summary>
    /// Módulos del sistema para asignar permisos
    /// </summary>
    public enum ModuloSupervisor
    {
        Contenido,
        Reportes,
        Verificacion,
        Usuarios,
        Estadisticas
    }
}
