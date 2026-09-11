/*
   EMORA 07-09-2026 Tabla de detalle para el informe matinal de existencias en revisión
*/

IF OBJECT_ID('dbo.ControlVencimientoRevisionInforme', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ControlVencimientoRevisionInforme
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ControlVencimientoRevisionInforme PRIMARY KEY,
        EjecucionLogId INT NOT NULL,
        ExistenciaId INT NOT NULL,
        EstadoAnterior INT NOT NULL,
        ProductoCodigo VARCHAR(100) NOT NULL,
        LoteCodigo VARCHAR(100) NOT NULL,
        FechaVencimiento DATETIME NOT NULL,
        FechaEnvioRevision DATETIME NOT NULL,
        FechaProcesamiento DATETIME NOT NULL
            CONSTRAINT DF_ControlVencimientoRevisionInforme_Fecha DEFAULT GETDATE(),
        CorreoEnviado BIT NOT NULL
            CONSTRAINT DF_ControlVencimientoRevisionInforme_Correo DEFAULT 0,
        FechaCorreo DATETIME NULL
    );

    CREATE INDEX IX_ControlVencimientoRevisionInforme_Pendiente
        ON dbo.ControlVencimientoRevisionInforme(CorreoEnviado, Id);
END;
GO
