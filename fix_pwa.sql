UPDATE Popups SET 
    EsPWA = 1,
    SoloSiInstalable = 1,
    MostrarUsuariosAnonimos = 1,
    MostrarUsuariosLogueados = 1,
    Contenido = N'<p>Accede rapidamente desde tu pantalla de inicio. Instala nuestra app para una mejor experiencia.</p>',
    UltimaModificacion = GETDATE()
WHERE Id = 1
