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
        ,P.PRO_Codigo AS ProductoCodigo
        ,S.LOT_Id AS LoteId
        ,L.LOT_Codigo AS LoteCodigo
        ,S.CLI_Id AS ClienteId
        ,C.CLI_RazonSocial AS ClienteNombre
        ,S.SOLLOTRES_CantidadProductos AS CantidadProductos
        ,S.SOLLOTRES_FechaCreacion AS FechaCreacion
        ,S.SOLLOTRES_DiasVigencia AS DiasVigencia
        ,DATEADD(DAY, S.SOLLOTRES_DiasVigencia + 1, S.SOLLOTRES_FechaCreacion)
            AS FechaCaducidad
        ,ISNULL(S.SOLLOTRES_CantidadRestante, 0) AS CantidadRestante
    FROM dbo.SOLLOTRES_SolicitudLoteReservado S
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = S.PRO_Id
    INNER JOIN dbo.LOT_Lote L ON L.LOT_Id = S.LOT_Id
    INNER JOIN dbo.CLI_Cliente C ON C.CLI_Id = S.CLI_Id
    WHERE S.SOLLOTRES_Activo = 1
      AND S.SOLLOTRES_Estado IN (1,2)
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
