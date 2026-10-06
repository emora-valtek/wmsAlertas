/*
    EMORA 06-10-2026 Se crea SP para obtener el detalle de peso acumulado de
    productos peligrosos con existencias activas en San Bernardo.
*/
CREATE OR ALTER PROCEDURE dbo.spAlertaProductoPeligrosoPesoDetalleObtener
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
         CASE WHEN P.PRO_EsInflamable = 1 THEN 'Inflamable' ELSE 'Peligroso' END AS Atributo
        ,P.PRO_Codigo AS Codigo
        ,P.PRO_Nombre AS Nombre
        ,COALESCE(NULLIF(N.NIV_Alias, ''), 'Sin ubicación asignada') AS Ubicacion
        ,dbo.fnEstadoExistenciaDescripcion(X.XTN_Estado) AS Estado
        ,P.PRO_Peso AS PesoUnitario
        ,COUNT(*) AS CantidadExistencias
        ,SUM(ISNULL(P.PRO_Peso, 0)) AS PesoTotal
    FROM dbo.PRO_Producto P
    INNER JOIN dbo.XTN_Existencia X
        ON X.PRO_Id = P.PRO_Id
       AND X.XTN_Activo = 1
       AND X.XTN_Estado IN (0, 1, 2, 10, 11, 14)
    INNER JOIN dbo.ALM_Almacenamiento A
        ON A.ALM_Id = X.ALM_Id
       AND A.BOD_Id = 1 -- Bodega física San Bernardo
    LEFT JOIN dbo.NIV_Nivel N
        ON N.NIV_Id = X.NIV_Id
    WHERE P.PRO_Activo = 1
      AND EXISTS
      (
          SELECT 1
          FROM dbo.PROATR_ProductoAtributo PA
          WHERE PA.PRO_Id = P.PRO_Id
            AND PA.TATR_Id = 4039
      )
    GROUP BY
         CASE WHEN P.PRO_EsInflamable = 1 THEN 'Inflamable' ELSE 'Peligroso' END
        ,P.PRO_Codigo
        ,P.PRO_Nombre
        ,COALESCE(NULLIF(N.NIV_Alias, ''), 'Sin ubicación asignada')
        ,X.XTN_Estado
        ,P.PRO_Peso
    ORDER BY Atributo, Codigo, Ubicacion, Estado;
END;
