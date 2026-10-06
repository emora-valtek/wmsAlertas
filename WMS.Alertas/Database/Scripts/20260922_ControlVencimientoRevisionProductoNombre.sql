/*
   EMORA 22-09-2026 Agrega nombre del producto al informe de existencias enviadas a revisión.
*/

IF COL_LENGTH('dbo.ControlVencimientoRevisionInforme', 'ProductoNombre') IS NULL
BEGIN
    ALTER TABLE dbo.ControlVencimientoRevisionInforme
        ADD ProductoNombre VARCHAR(300) NULL;
END;

UPDATE I
SET ProductoNombre = P.PRO_Nombre
FROM dbo.ControlVencimientoRevisionInforme I
INNER JOIN dbo.PRO_Producto P ON P.PRO_Codigo = I.ProductoCodigo
WHERE I.ProductoNombre IS NULL;
GO
