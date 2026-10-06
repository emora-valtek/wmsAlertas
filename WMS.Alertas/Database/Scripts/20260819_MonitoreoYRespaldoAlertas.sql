/*
    Ejecutar antes de publicar la versión de WMS.Alertas que incorpora el
    respaldo de Packing List y la notificación al encargado.

    Los parámetros nuevos tienen valores predeterminados para conservar la
    compatibilidad con consumidores existentes.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.spAlertaPackingListPendientesTomar
(
     @ProcesadoPor          UNIQUEIDENTIFIER
    ,@CantidadMaxima        INT = 50
    ,@MinutosAntiguedad     INT = 0
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ProcesadoPor IS NULL
    BEGIN
        RAISERROR('[BusinessException] Debe indicar el identificador del proceso.', 16, 1);
        RETURN;
    END;

    IF ISNULL(@CantidadMaxima, 0) NOT BETWEEN 1 AND 500
    BEGIN
        RAISERROR('[BusinessException] La cantidad maxima debe estar entre 1 y 500.', 16, 1);
        RETURN;
    END;

    IF ISNULL(@MinutosAntiguedad, -1) < 0
    BEGIN
        RAISERROR('[BusinessException] La antiguedad no puede ser negativa.', 16, 1);
        RETURN;
    END;

    ;WITH Pendientes AS
    (
        SELECT TOP (@CantidadMaxima)
            A.AlertaPackingListId
        FROM dbo.AlertaPackingList A WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE A.Estado = 'PENDIENTE'
          AND A.FechaProximoIntento <= SYSDATETIME()
          AND A.FechaEvento <= DATEADD(MINUTE, -@MinutosAntiguedad, SYSDATETIME())
        ORDER BY
             A.FechaProximoIntento
            ,A.FechaEvento
            ,A.AlertaPackingListId
    )
    UPDATE A
    SET
         A.Estado = 'PROCESANDO'
        ,A.Intentos = A.Intentos + 1
        ,A.FechaProcesamiento = SYSDATETIME()
        ,A.ProcesadoPor = @ProcesadoPor
        ,A.MensajeError = NULL
    OUTPUT
         INSERTED.AlertaPackingListId   AS AlertaPackingListId
        ,INSERTED.EventoId              AS EventoId
        ,INSERTED.PackingListId         AS PackingListId
        ,INSERTED.DetallePackingListId  AS DetallePackingListId
        ,INSERTED.TipoModificacion      AS TipoModificacion
        ,INSERTED.ProductoId            AS ProductoId
        ,INSERTED.LoteAnteriorId        AS LoteAnteriorId
        ,INSERTED.LoteNuevoId           AS LoteNuevoId
        ,INSERTED.CantidadAnterior      AS CantidadAnterior
        ,INSERTED.CantidadNueva         AS CantidadNueva
        ,INSERTED.CantidadModificada    AS CantidadModificada
        ,INSERTED.Observacion           AS Observacion
        ,INSERTED.NumeroNotaVenta       AS NumeroNotaVenta
        ,INSERTED.ProductoCodigo        AS ProductoCodigo
        ,INSERTED.ProductoNombre        AS ProductoNombre
        ,INSERTED.LoteAnteriorCodigo    AS LoteAnteriorCodigo
        ,INSERTED.LoteNuevoCodigo       AS LoteNuevoCodigo
        ,INSERTED.VendedorId            AS VendedorId
        ,INSERTED.VendedorNombre        AS VendedorNombre
        ,INSERTED.CorreoVendedor        AS CorreoVendedor
        ,INSERTED.ModificadoPor         AS ModificadoPor
        ,INSERTED.ModificadoPorNombre   AS ModificadoPorNombre
        ,INSERTED.FechaEvento           AS FechaEvento
        ,INSERTED.Intentos              AS Intentos
    FROM dbo.AlertaPackingList A
    INNER JOIN Pendientes P
        ON P.AlertaPackingListId = A.AlertaPackingListId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spAlertaPackingListMarcarError
(
     @AlertaPackingListId       BIGINT
    ,@ProcesadoPor              UNIQUEIDENTIFIER
    ,@MensajeError              NVARCHAR(4000)
    ,@MaximoIntentos            INT = 5
    ,@ReintentarEnMinutos       INT = 5
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(@MaximoIntentos, 0) < 1
       OR ISNULL(@ReintentarEnMinutos, 0) < 1
    BEGIN
        RAISERROR('[BusinessException] La configuracion de reintentos no es valida.', 16, 1);
        RETURN;
    END;

    UPDATE dbo.AlertaPackingList
    SET
         Estado =
            CASE
                WHEN Intentos >= @MaximoIntentos THEN 'ERROR'
                ELSE 'PENDIENTE'
            END
        ,FechaProximoIntento =
            CASE
                WHEN Intentos >= @MaximoIntentos
                    THEN FechaProximoIntento
                ELSE DATEADD(MINUTE, @ReintentarEnMinutos, SYSDATETIME())
            END
        ,FechaProcesamiento = NULL
        ,ProcesadoPor = NULL
        ,MensajeError = LEFT(@MensajeError, 4000)
    OUTPUT INSERTED.Estado
    WHERE AlertaPackingListId = @AlertaPackingListId
      AND Estado = 'PROCESANDO'
      AND ProcesadoPor = @ProcesadoPor;

    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR(
            '[BusinessException] La alerta no esta siendo procesada por el proceso indicado.',
            16,
            1
        );
    END;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.AlertasCorreoDestino
    WHERE TipoAlerta = 'ErrorWMSAlertas'
)
BEGIN
    UPDATE dbo.AlertasCorreoDestino
    SET
         Nombre = 'Encargado WMS.Alertas'
        ,Email = 'emora@valtek.cl'
        ,Activo = 1
    WHERE TipoAlerta = 'ErrorWMSAlertas';
END
ELSE
BEGIN
    INSERT INTO dbo.AlertasCorreoDestino
    (
         TipoAlerta
        ,Nombre
        ,Email
        ,Activo
    )
    VALUES
    (
         'ErrorWMSAlertas'
        ,'Encargado WMS.Alertas'
        ,'emora@valtek.cl'
        ,1
    );
END;
GO
