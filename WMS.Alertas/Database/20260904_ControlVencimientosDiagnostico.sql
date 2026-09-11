/*
   EMORA 04-09-2026 SP obtiene datos para el control de vencimientos
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
      AND X.XTN_Estado IN (0, 1, 2, 14)
      AND P.PRO_MinimoVencimiento IS NOT NULL
      AND P.PRO_MaximoVencimiento IS NOT NULL
      AND L.LOT_FechaVencimiento IS NOT NULL
      AND DATEADD(DAY, -P.PRO_MinimoVencimiento, L.LOT_FechaVencimiento)
            < GETDATE()
    ORDER BY X.XTN_Id;
END;
GO
