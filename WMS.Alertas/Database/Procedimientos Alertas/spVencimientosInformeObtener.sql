/*
   EMORA 14-09-2026 SP obtiene existencias pendientes incluyendo su ubicación anterior
*/

CREATE OR ALTER PROCEDURE dbo.spVencimientosInformeObtener
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
         Id AS InformeId
        ,ExistenciaId
        ,EstadoAnterior AS Estado
        ,ProductoCodigo
        ,ProductoNombre
        ,LoteCodigo
        ,UbicacionAnterior
        ,FechaVencimiento
        ,FechaEnvioRevision
    FROM dbo.ControlVencimientoRevisionInforme
    WHERE CorreoEnviado = 0
    ORDER BY Id;
END;
GO
