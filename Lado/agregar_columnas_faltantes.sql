-- Script para agregar columnas faltantes a AspNetUsers
-- Ejecutar en la base de datos de Lado

-- Columnas de Boost
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'BoostActivo')
    ALTER TABLE AspNetUsers ADD BoostActivo BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'BoostCredito')
    ALTER TABLE AspNetUsers ADD BoostCredito DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'BoostFechaFin')
    ALTER TABLE AspNetUsers ADD BoostFechaFin DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'BoostMultiplicador')
    ALTER TABLE AspNetUsers ADD BoostMultiplicador DECIMAL(18,2) NOT NULL DEFAULT 1.0;

-- Columnas de Email Preferences
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailComentarios')
    ALTER TABLE AspNetUsers ADD EmailComentarios BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailConsejos')
    ALTER TABLE AspNetUsers ADD EmailConsejos BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailMenciones')
    ALTER TABLE AspNetUsers ADD EmailMenciones BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailNuevasSuscripciones')
    ALTER TABLE AspNetUsers ADD EmailNuevasSuscripciones BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailNuevoContenido')
    ALTER TABLE AspNetUsers ADD EmailNuevoContenido BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailNuevosMensajes')
    ALTER TABLE AspNetUsers ADD EmailNuevosMensajes BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailNuevosSeguidores')
    ALTER TABLE AspNetUsers ADD EmailNuevosSeguidores BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailPropinas')
    ALTER TABLE AspNetUsers ADD EmailPropinas BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailReporteGanancias')
    ALTER TABLE AspNetUsers ADD EmailReporteGanancias BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailResumenSemanal')
    ALTER TABLE AspNetUsers ADD EmailResumenSemanal BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'EmailStories')
    ALTER TABLE AspNetUsers ADD EmailStories BIT NOT NULL DEFAULT 1;

-- Columnas de Privacidad
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'MostrarEnBusquedas')
    ALTER TABLE AspNetUsers ADD MostrarEnBusquedas BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'MostrarSeguidores')
    ALTER TABLE AspNetUsers ADD MostrarSeguidores BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'MostrarSiguiendo')
    ALTER TABLE AspNetUsers ADD MostrarSiguiendo BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'PerfilPrivado')
    ALTER TABLE AspNetUsers ADD PerfilPrivado BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'PermitirEtiquetas')
    ALTER TABLE AspNetUsers ADD PermitirEtiquetas BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'QuienPuedeComentar')
    ALTER TABLE AspNetUsers ADD QuienPuedeComentar INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'QuienPuedeMensajear')
    ALTER TABLE AspNetUsers ADD QuienPuedeMensajear INT NOT NULL DEFAULT 0;

-- Columna de Saldo Publicitario
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'SaldoPublicitario')
    ALTER TABLE AspNetUsers ADD SaldoPublicitario DECIMAL(18,2) NOT NULL DEFAULT 0;

PRINT 'Columnas agregadas correctamente a AspNetUsers';
