/*
    Datos controlados para probar ControlVencimientos en una base local/QA.

    Por seguridad el script hace ROLLBACK por defecto. Revise primero la salida y
    cambie @Confirmar a 1 solamente en la base de pruebas donde ejecutará el job.
    CantidadRestante queda en cero para que la caducidad no modifique stock.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Confirmar BIT = 0;
DECLARE @UsuarioId INT = TRY_CAST((SELECT TOP (1) CGS_Valor
    FROM dbo.CGS_ConfiguracionSistema WHERE CGS_Codigo = 'JOB_USUID') AS INT);
DECLARE @ClienteId INT = (SELECT TOP (1) CLI_Id
    FROM dbo.CLI_Cliente WHERE CLI_Activo = 1 ORDER BY CLI_Id);
DECLARE @ProductoVigenciaId INT;
DECLARE @LoteVigenciaId INT;
DECLARE @ProductoVencidoId INT;
DECLARE @LoteVencidoId INT;
DECLARE @SolicitudVigenciaId INT;
DECLARE @SolicitudLoteVencidoId INT;

SELECT TOP (1)
    @ProductoVigenciaId = P.PRO_Id,
    @LoteVigenciaId = L.LOT_Id
FROM dbo.LOT_Lote L
INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = L.PRO_Id
WHERE L.LOT_Activo = 1 AND P.PRO_Activo = 1
ORDER BY L.LOT_Id;

SELECT TOP (1)
    @ProductoVencidoId = P.PRO_Id,
    @LoteVencidoId = L.LOT_Id
FROM dbo.LOT_Lote L
INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = L.PRO_Id
WHERE L.LOT_Activo = 1
  AND P.PRO_Activo = 1
  AND P.PRO_MaximoVencimiento IS NOT NULL
  AND L.LOT_FechaVencimiento < GETDATE()
ORDER BY L.LOT_FechaVencimiento, L.LOT_Id;

IF @UsuarioId IS NULL OR @ClienteId IS NULL
    THROW 51001, 'Falta JOB_USUID o un cliente activo para crear los datos de prueba.', 1;
IF @ProductoVigenciaId IS NULL OR @LoteVigenciaId IS NULL
    THROW 51002, 'No existe un producto/lote activo para la prueba de vigencia.', 1;
IF @ProductoVencidoId IS NULL OR @LoteVencidoId IS NULL
    THROW 51003, 'No existe un producto con MaximoVencimiento y lote activo ya vencido.', 1;

BEGIN TRANSACTION;

INSERT dbo.SOLLOTRES_SolicitudLoteReservado
(
    CLI_Id, PRO_Id, LOT_Id, SOLLOTRES_CantidadProductos,
    SOLLOTRES_CantidadMinimaInformar, SOLLOTRES_Estado,
    SOLLOTRES_CantidadRestante, SOLLOTRES_CreadoPor,
    SOLLOTRES_FechaCreacion, SOLLOTRES_Accesibilidad,
    SOLLOTRES_Activo, SOLLOTRES_DiasVigencia
)
VALUES
(
    @ClienteId, @ProductoVigenciaId, @LoteVigenciaId, 1,
    1, 1, 0, @UsuarioId, DATEADD(DAY, -10, GETDATE()), 1, 1, 1
);
SET @SolicitudVigenciaId = SCOPE_IDENTITY();

INSERT dbo.SOLLOTRES_SolicitudLoteReservado
(
    CLI_Id, PRO_Id, LOT_Id, SOLLOTRES_CantidadProductos,
    SOLLOTRES_CantidadMinimaInformar, SOLLOTRES_Estado,
    SOLLOTRES_CantidadRestante, SOLLOTRES_CreadoPor,
    SOLLOTRES_FechaCreacion, SOLLOTRES_Accesibilidad,
    SOLLOTRES_Activo, SOLLOTRES_DiasVigencia
)
VALUES
(
    @ClienteId, @ProductoVencidoId, @LoteVencidoId, 1,
    1, 3, 0, @UsuarioId, GETDATE(), 1, 1, 30
);
SET @SolicitudLoteVencidoId = SCOPE_IDENTITY();

SELECT
    @SolicitudVigenciaId AS SolicitudVigenciaVencidaId,
    @SolicitudLoteVencidoId AS SolicitudConLoteVencidoId,
    @ClienteId AS ClienteId,
    @ProductoVigenciaId AS ProductoVigenciaId,
    @LoteVigenciaId AS LoteVigenciaId,
    @ProductoVencidoId AS ProductoVencidoId,
    @LoteVencidoId AS LoteVencidoId,
    CASE WHEN @Confirmar = 1 THEN 'DATOS CONFIRMADOS' ELSE 'SOLO SIMULACION: ROLLBACK' END AS Resultado;

IF @Confirmar = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;
