/*
   EMORA 07-09-2026 SP obtiene solicitudes y lotes reservados por vencimiento para Alerta
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.spLoteReservadosVencimientoObtener
AS
BEGIN
    SET NOCOUNT ON;

    /* Solicitudes creadas/asignadas cuya vigencia ya terminó. */
    SELECT
         S.SOLLOTRES_Id AS SolicitudLoteReservadoId
        ,S.SOLLOTRES_Estado AS Estado
        ,S.PRO_Id AS ProductoId
        ,P.PRO_Codigo AS ProductoCodigo
        ,S.LOT_Id AS LoteId
        ,L.LOT_Codigo AS LoteCodigo
        ,S.CLI_Id AS ClienteId
        ,C.CLI_RazonSocial AS ClienteNombre
        ,ISNULL(LTRIM(RTRIM(U.USU_Correo)), '') AS SolicitanteCorreo
        ,S.SOLLOTRES_CantidadProductos AS CantidadProductos
        ,S.SOLLOTRES_FechaCreacion AS FechaCreacion
        ,S.SOLLOTRES_DiasVigencia AS DiasVigencia
        ,DATEADD(DAY, S.SOLLOTRES_DiasVigencia + 1, S.SOLLOTRES_FechaCreacion) AS FechaCaducidad
        ,ISNULL(S.SOLLOTRES_CantidadRestante, 0) AS CantidadRestante
    FROM dbo.SOLLOTRES_SolicitudLoteReservado S
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = S.PRO_Id
    INNER JOIN dbo.LOT_Lote L ON L.LOT_Id = S.LOT_Id
    INNER JOIN dbo.CLI_Cliente C ON C.CLI_Id = S.CLI_Id
    LEFT JOIN dbo.USU_Usuario U ON U.USU_Id = S.SOLLOTRES_CreadoPor
    WHERE S.SOLLOTRES_Activo = 1
      AND S.SOLLOTRES_Estado IN (1, 2)
      AND S.SOLLOTRES_DiasVigencia IS NOT NULL
      AND S.SOLLOTRES_FechaCreacion IS NOT NULL
      AND DATEADD(DAY, S.SOLLOTRES_DiasVigencia + 1, S.SOLLOTRES_FechaCreacion) <= GETDATE()
    ORDER BY FechaCaducidad, S.SOLLOTRES_Id;

    /* Reservas confirmadas próximas a vencer o con el lote ya vencido. */
    SELECT
         S.SOLLOTRES_Id AS SolicitudLoteReservadoId
        ,S.SOLLOTRES_Estado AS Estado
        ,S.PRO_Id AS ProductoId
        ,P.PRO_Codigo AS ProductoCodigo
        ,S.LOT_Id AS LoteId
        ,L.LOT_Codigo AS LoteCodigo
        ,S.CLI_Id AS ClienteId
        ,C.CLI_RazonSocial AS ClienteNombre
        ,ISNULL(LTRIM(RTRIM(U.USU_Correo)), '') AS SolicitanteCorreo
        ,S.SOLLOTRES_CantidadProductos AS CantidadProductos
        ,L.LOT_FechaVencimiento AS FechaVencimiento
        ,P.PRO_MaximoVencimiento AS MaximoVencimiento
        ,DATEADD(DAY, -P.PRO_MaximoVencimiento, L.LOT_FechaVencimiento) AS FechaInicioAviso
        ,ISNULL(S.SOLLOTRES_CantidadRestante, 0) AS CantidadRestante
        ,CASE
            WHEN L.LOT_FechaVencimiento < GETDATE() THEN 'VENCIDO'
            ELSE 'PROXIMO_A_VENCER'
         END AS Situacion
    FROM dbo.SOLLOTRES_SolicitudLoteReservado S
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = S.PRO_Id
    INNER JOIN dbo.LOT_Lote L ON L.LOT_Id = S.LOT_Id
    INNER JOIN dbo.CLI_Cliente C ON C.CLI_Id = S.CLI_Id
    LEFT JOIN dbo.USU_Usuario U ON U.USU_Id = S.SOLLOTRES_CreadoPor
    WHERE S.SOLLOTRES_Activo = 1
      AND S.SOLLOTRES_Estado = 3
      AND P.PRO_Activo = 1
      AND L.LOT_Activo = 1
      AND P.PRO_MaximoVencimiento IS NOT NULL
      AND L.LOT_FechaVencimiento IS NOT NULL
      AND DATEADD(DAY, -P.PRO_MaximoVencimiento, L.LOT_FechaVencimiento) < GETDATE()
    ORDER BY L.LOT_FechaVencimiento, S.SOLLOTRES_Id;
END;
GO
