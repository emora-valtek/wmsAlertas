/*
   EMORA 07-09-2026 SP marca una existencia como incluida en el informe matinal
*/

CREATE OR ALTER PROCEDURE dbo.spVencimientosInformeMarcar
    @InformeId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ControlVencimientoRevisionInforme
    SET CorreoEnviado = 1,
        FechaCorreo = GETDATE()
    WHERE Id = @InformeId
      AND CorreoEnviado = 0;
END;
GO
