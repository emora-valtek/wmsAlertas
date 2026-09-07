/*
   EMORA 04-09-2026 SP envía a revisión existencias próximas a vencer, desde las tareas automáticas
*/

CREATE OR ALTER PROCEDURE dbo.spVencimientosRevisionar
    @CantidadMaxima INT = 500,
    @EjecucionLogId INT
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
        X.LOT_Id LoteId, X.XTN_Estado EstadoAnterior,
        P.PRO_Codigo ProductoCodigo, L.LOT_Codigo LoteCodigo,
        L.LOT_FechaVencimiento FechaVencimiento,
        DATEADD(DAY,-P.PRO_MinimoVencimiento,L.LOT_FechaVencimiento) FechaEnvioRevision
    INTO #Cambios
    FROM dbo.XTN_Existencia X WITH (UPDLOCK, READPAST, ROWLOCK)
    JOIN dbo.PRO_Producto P ON P.PRO_Id=X.PRO_Id AND P.PRO_Activo=1
    JOIN dbo.LOT_Lote L ON L.LOT_Id=X.LOT_Id AND L.LOT_Activo=1
    WHERE X.XTN_Activo=1 AND X.XTN_Estado IN (0,1,2,14)
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

        INSERT dbo.ControlVencimientoRevisionInforme
        (
            EjecucionLogId, ExistenciaId, EstadoAnterior, ProductoCodigo,
            LoteCodigo, FechaVencimiento, FechaEnvioRevision
        )
        SELECT @EjecucionLogId, ExistenciaId, EstadoAnterior, ProductoCodigo,
            LoteCodigo, FechaVencimiento, FechaEnvioRevision
        FROM #Cambios;
    END
    COMMIT;

    DECLARE @Pendientes INT=(SELECT COUNT(*) FROM dbo.XTN_Existencia X
      JOIN dbo.PRO_Producto P ON P.PRO_Id=X.PRO_Id AND P.PRO_Activo=1
      JOIN dbo.LOT_Lote L ON L.LOT_Id=X.LOT_Id AND L.LOT_Activo=1
      WHERE X.XTN_Activo=1 AND X.XTN_Estado IN (0,1,2,14)
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
