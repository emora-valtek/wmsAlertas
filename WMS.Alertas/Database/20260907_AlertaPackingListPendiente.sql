/*
    EMORA 03-08-2026 Sp obtiene PL pendientes de recolectar para Alerta
*/

CREATE OR ALTER PROCEDURE dbo.spPackingListPendienteObtener
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
         PL.DOCEGPL_Id AS PackingListId
        ,PL.DOCEGPL_Estado AS Estado
        ,CASE PL.DOCEGPL_Estado
            WHEN 1 THEN 'Pendiente de asignar'
            WHEN 2 THEN 'Usuario asignado'
            WHEN 3 THEN 'En recolección'
            WHEN 4 THEN 'Recolectado'
            WHEN 10 THEN 'Transporte asignado'
         END AS EstadoDescripcion
        ,PL.DOCEGPL_FechaCreacion AS FechaCreacion
        ,DATEDIFF(MINUTE, PL.DOCEGPL_FechaCreacion, GETDATE()) / 1440
            AS DiasTranscurridos
        ,CASE
            WHEN PL.DOCEGPL_Estado IN (1, 2, 10) THEN 'RECOLECCION'
            ELSE 'EMBALAJE'
         END AS Etapa
    FROM dbo.DOCEGPL_DocumentoEgresoPackingList PL
    WHERE PL.DOCEGPL_Activo = 1
      AND PL.DOCEGPL_Estado IN (1, 2, 3, 4, 10)
      AND DATEADD(DAY, 2, PL.DOCEGPL_FechaCreacion) < GETDATE()
    ORDER BY Etapa, PL.DOCEGPL_FechaCreacion, PL.DOCEGPL_Id;
END;
GO
