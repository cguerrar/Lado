-- Script para inicializar configuraciones de email en ConfiguracionPlataforma
-- Ejecutar si las configuraciones no existen

-- Verificar y agregar configuraciones de email
IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'Email_ProveedorActivo')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('Email_ProveedorActivo', 'Mailjet', 'Proveedor de email activo (Mailjet o AmazonSES)', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'Email_FromEmail')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('Email_FromEmail', 'noreply@ladoapp.com', 'Email del remitente', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'Email_FromName')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('Email_FromName', 'Lado', 'Nombre del remitente', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'Mailjet_ApiKey')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('Mailjet_ApiKey', '', 'API Key de Mailjet', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'Mailjet_SecretKey')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('Mailjet_SecretKey', '', 'Secret Key de Mailjet', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'AmazonSES_AccessKey')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('AmazonSES_AccessKey', '', 'Access Key de Amazon SES', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'AmazonSES_SecretKey')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('AmazonSES_SecretKey', '', 'Secret Key de Amazon SES', 'Email', GETDATE());

IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPlataforma WHERE Clave = 'AmazonSES_Region')
    INSERT INTO ConfiguracionesPlataforma (Clave, Valor, Descripcion, Categoria, UltimaModificacion)
    VALUES ('AmazonSES_Region', 'us-east-1', 'Region de Amazon SES', 'Email', GETDATE());

-- Verificar que se insertaron
SELECT * FROM ConfiguracionesPlataforma WHERE Categoria = 'Email';
