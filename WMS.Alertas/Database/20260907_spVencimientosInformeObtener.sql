/*
   EMORA 07-09-2026 SP obtiene existencias pendientes del informe matinal
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
        ,LoteCodigo
        ,FechaVencimiento
        ,FechaEnvioRevision
    FROM dbo.ControlVencimientoRevisionInforme
    WHERE CorreoEnviado = 0
    ORDER BY Id;
END;
GO
