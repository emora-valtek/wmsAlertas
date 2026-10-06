/*
   EMORA 14-09-2026 Agrega la ubicación anterior al informe de existencias enviadas a revisión
*/

IF COL_LENGTH('dbo.ControlVencimientoRevisionInforme', 'UbicacionAnterior') IS NULL
BEGIN
    ALTER TABLE dbo.ControlVencimientoRevisionInforme
        ADD UbicacionAnterior VARCHAR(100) NULL;
END;
GO
