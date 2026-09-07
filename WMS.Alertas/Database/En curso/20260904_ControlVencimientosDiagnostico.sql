/*
    Diagnóstico, sin escrituras, para la migración de VerificarEstadoExistencias.
    Devuelve tres conjuntos: existencias para revisión, reservas vencidas por
    vigencia y reservas próximas a vencer/vencidas por fecha del lote.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.spVencimientosObtener
AS
BEGIN
    SET NOCOUNT ON;

    /* 1. Existencias que alcanzarían el umbral para enviarse a revisión. */
    SELECT
         X.XTN_Id AS ExistenciaId
        ,X.XTN_Estado AS Estado
        ,X.PRO_Id AS ProductoId
        ,P.PRO_Codigo AS ProductoCodigo
        ,X.LOT_Id AS LoteId
        ,L.LOT_Codigo AS LoteCodigo
        ,L.LOT_FechaVencimiento AS FechaVencimiento
        ,P.PRO_MinimoVencimiento AS MinimoVencimiento
        ,DATEADD(DAY, -P.PRO_MinimoVencimiento, L.LOT_FechaVencimiento)
            AS FechaEnvioRevision
    FROM dbo.XTN_Existencia X
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = X.PRO_Id
    INNER JOIN dbo.LOT_Lote L ON L.LOT_Id = X.LOT_Id
    WHERE X.XTN_Activo = 1
      AND P.PRO_Activo = 1
      AND L.LOT_Activo = 1
      AND X.XTN_Estado IN (0, 1, 2, 14, 20, 22)
      AND P.PRO_MinimoVencimiento IS NOT NULL
      AND P.PRO_MaximoVencimiento IS NOT NULL
      AND L.LOT_FechaVencimiento IS NOT NULL
      AND DATEADD(DAY, -P.PRO_MinimoVencimiento, L.LOT_FechaVencimiento)
            < GETDATE()
    ORDER BY X.XTN_Id;

    /* 2. Solicitudes creadas/asignadas cuya vigencia ya terminó. Se replica
       la regla antigua: deben haber transcurrido más de DiasVigencia días
       completos, no solamente haber cambiado la fecha calendario. */
    SELECT
         S.SOLLOTRES_Id AS SolicitudLoteReservadoId
        ,S.SOLLOTRES_Estado AS Estado
        ,S.PRO_Id AS ProductoId
        ,S.LOT_Id AS LoteId
        ,S.CLI_Id AS ClienteId
        ,C.CLI_RazonSocial AS ClienteNombre
        ,S.SOLLOTRES_CantidadProductos AS CantidadProductos
        ,S.SOLLOTRES_FechaCreacion AS FechaCreacion
        ,S.SOLLOTRES_DiasVigencia AS DiasVigencia
        ,DATEADD(DAY, S.SOLLOTRES_DiasVigencia + 1, S.SOLLOTRES_FechaCreacion)
            AS FechaCaducidad
        ,ISNULL(S.SOLLOTRES_CantidadRestante, 0) AS CantidadRestante
    FROM dbo.SOLLOTRES_SolicitudLoteReservado S
    INNER JOIN dbo.CLI_Cliente C ON C.CLI_Id = S.CLI_Id
    WHERE S.SOLLOTRES_Activo = 1
      AND S.SOLLOTRES_Estado IN (1, 2)
      AND S.SOLLOTRES_DiasVigencia IS NOT NULL
      AND S.SOLLOTRES_FechaCreacion IS NOT NULL
      AND DATEADD(DAY, S.SOLLOTRES_DiasVigencia + 1, S.SOLLOTRES_FechaCreacion)
            <= GETDATE()
    ORDER BY FechaCaducidad, S.SOLLOTRES_Id;

    /* 3. Las reservas confirmadas se avisan usando MaximoVencimiento, pero
       solamente se caducarán cuando la fecha real del lote ya haya pasado. */
    SELECT
         S.SOLLOTRES_Id AS SolicitudLoteReservadoId
        ,S.SOLLOTRES_Estado AS Estado
        ,S.PRO_Id AS ProductoId
        ,P.PRO_Codigo AS ProductoCodigo
        ,S.LOT_Id AS LoteId
        ,L.LOT_Codigo AS LoteCodigo
        ,S.CLI_Id AS ClienteId
        ,C.CLI_RazonSocial AS ClienteNombre
        ,S.SOLLOTRES_CantidadProductos AS CantidadProductos
        ,L.LOT_FechaVencimiento AS FechaVencimiento
        ,P.PRO_MaximoVencimiento AS MaximoVencimiento
        ,DATEADD(DAY, -P.PRO_MaximoVencimiento, L.LOT_FechaVencimiento)
            AS FechaInicioAviso
        ,ISNULL(S.SOLLOTRES_CantidadRestante, 0) AS CantidadRestante
        ,CASE
            WHEN L.LOT_FechaVencimiento < GETDATE() THEN 'VENCIDO'
            ELSE 'PROXIMO_A_VENCER'
         END AS Situacion
    FROM dbo.SOLLOTRES_SolicitudLoteReservado S
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = S.PRO_Id
    INNER JOIN dbo.LOT_Lote L ON L.LOT_Id = S.LOT_Id
    INNER JOIN dbo.CLI_Cliente C ON C.CLI_Id = S.CLI_Id
    WHERE S.SOLLOTRES_Activo = 1
      AND S.SOLLOTRES_Estado = 3
      AND P.PRO_Activo = 1
      AND L.LOT_Activo = 1
      AND P.PRO_MaximoVencimiento IS NOT NULL
      AND L.LOT_FechaVencimiento IS NOT NULL
      AND DATEADD(DAY, -P.PRO_MaximoVencimiento, L.LOT_FechaVencimiento)
            < GETDATE()
    ORDER BY L.LOT_FechaVencimiento, S.SOLLOTRES_Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spVencimientosRevisionar
    @CantidadMaxima INT = 500
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(@CantidadMaxima, 0) NOT BETWEEN 1 AND 1000
        THROW 50001, 'CantidadMaxima debe estar entre 1 y 1000.', 1;

    DECLARE @UsuarioId INT = TRY_CAST((SELECT TOP (1) CGS_Valor
        FROM dbo.CGS_ConfiguracionSistema WHERE CGS_Codigo = 'JOB_USUID') AS INT);
    IF @UsuarioId IS NULL
        THROW 50002, 'JOB_USUID no está configurado correctamente.', 1;

    BEGIN TRY
    BEGIN TRANSACTION;
    SELECT TOP (@CantidadMaxima) X.XTN_Id ExistenciaId, X.PRO_Id ProductoId,
        X.LOT_Id LoteId, X.XTN_Estado EstadoAnterior
    INTO #Cambios
    FROM dbo.XTN_Existencia X WITH (UPDLOCK, READPAST, ROWLOCK)
    JOIN dbo.PRO_Producto P ON P.PRO_Id=X.PRO_Id AND P.PRO_Activo=1
    JOIN dbo.LOT_Lote L ON L.LOT_Id=X.LOT_Id AND L.LOT_Activo=1
    WHERE X.XTN_Activo=1 AND X.XTN_Estado IN (0,1,2,14,20,22)
      AND P.PRO_MinimoVencimiento IS NOT NULL AND P.PRO_MaximoVencimiento IS NOT NULL
      AND DATEADD(DAY,-P.PRO_MinimoVencimiento,L.LOT_FechaVencimiento)<GETDATE()
    ORDER BY X.XTN_Id;
    DECLARE @Procesadas INT=@@ROWCOUNT;

    IF @Procesadas>0
    BEGIN
        EXEC sys.sp_set_session_context @key=N'OmitirTriggerStock',@value=1;
        UPDATE X SET XTN_Estado=11,MREV_Id=9000,XTN_ModificadoPor=@UsuarioId,
            XTN_FechaModificacion=GETDATE()
        FROM dbo.XTN_Existencia X JOIN #Cambios C ON C.ExistenciaId=X.XTN_Id
        WHERE X.XTN_Estado=C.EstadoAnterior;
        EXEC sys.sp_set_session_context @key=N'OmitirTriggerStock',@value=NULL;

        INSERT dbo.StockMovimiento(Fecha,UsuarioId,ExistenciaId,ProductoId,LoteId,
            NivelMovimiento,TipoMovimiento,Proceso,Cantidad,DocumentoOrigenTipo,DocumentoOrigenId)
        SELECT GETDATE(),@UsuarioId,ExistenciaId,ProductoId,LoteId,'EXISTENCIA',
            CASE WHEN EstadoAnterior=2 THEN 'SALIDA' ELSE 'PROCESO_INTERNO' END,
            'ENVIAR_REVISION',1,'REVISION_AUTOMATICA',ExistenciaId FROM #Cambios;

        INSERT dbo.StockMovimiento(Fecha,UsuarioId,ExistenciaId,ProductoId,LoteId,
            NivelMovimiento,TipoMovimiento,Proceso,Cantidad,DocumentoOrigenTipo,DocumentoOrigenId)
        SELECT GETDATE(),@UsuarioId,NULL,ProductoId,LoteId,'PRODUCTO',
            CASE WHEN EstadoAnterior=2 THEN 'SALIDA' ELSE 'PROCESO_INTERNO' END,
            'ENVIAR_REVISION',COUNT(*),'REVISION_AUTOMATICA',NULL
        FROM #Cambios GROUP BY ProductoId,LoteId,EstadoAnterior;

        UPDATE PLC SET
          PROLOTCANT_Recepcionados=CASE WHEN ISNULL(PROLOTCANT_Recepcionados,0)>=A.R THEN ISNULL(PROLOTCANT_Recepcionados,0)-A.R ELSE 0 END,
          PROLOTCANT_PendientesIngreso=CASE WHEN ISNULL(PROLOTCANT_PendientesIngreso,0)>=A.P THEN ISNULL(PROLOTCANT_PendientesIngreso,0)-A.P ELSE 0 END,
          PROLOTCANT_Disponibles=CASE WHEN ISNULL(PROLOTCANT_Disponibles,0)>=A.D THEN ISNULL(PROLOTCANT_Disponibles,0)-A.D ELSE 0 END,
          PROLOTCANT_ModificadoPor=@UsuarioId,PROLOTCANT_FechaModificacion=GETDATE()
        FROM dbo.PROLOTCANT_ProductoLoteCantidad PLC JOIN (
          SELECT ProductoId,LoteId,SUM(CASE WHEN EstadoAnterior=0 THEN 1 ELSE 0 END) R,
          SUM(CASE WHEN EstadoAnterior=1 THEN 1 ELSE 0 END) P,
          SUM(CASE WHEN EstadoAnterior=2 THEN 1 ELSE 0 END) D FROM #Cambios GROUP BY ProductoId,LoteId
        ) A ON A.ProductoId=PLC.PRO_Id AND A.LoteId=PLC.LOT_Id WHERE PLC.PROLOTCANT_Activo=1;
    END
    COMMIT;

    DECLARE @Pendientes INT=(SELECT COUNT(*) FROM dbo.XTN_Existencia X
      JOIN dbo.PRO_Producto P ON P.PRO_Id=X.PRO_Id AND P.PRO_Activo=1
      JOIN dbo.LOT_Lote L ON L.LOT_Id=X.LOT_Id AND L.LOT_Activo=1
      WHERE X.XTN_Activo=1 AND X.XTN_Estado IN (0,1,2,14,20,22)
      AND P.PRO_MinimoVencimiento IS NOT NULL AND P.PRO_MaximoVencimiento IS NOT NULL
      AND DATEADD(DAY,-P.PRO_MinimoVencimiento,L.LOT_FechaVencimiento)<GETDATE());
    SELECT @Procesadas Procesadas,@Pendientes Pendientes;
    END TRY
    BEGIN CATCH
        EXEC sys.sp_set_session_context @key=N'OmitirTriggerStock',@value=NULL;
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
