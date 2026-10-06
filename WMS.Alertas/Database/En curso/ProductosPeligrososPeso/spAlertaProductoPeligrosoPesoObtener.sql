/*
    EMORA 06-10-2026 Se crea SP para obtener anomalías de peso de productos peligrosos
    con existencias activas de estados 0, 1, 2, 10, 11 y 14 en San Bernardo,
    y controlar límites acumulados.

    Obtiene anomalías de peso de productos con el atributo de almacenamiento
    "Peligroso" (TATR_Id = 4039).
*/
CREATE OR ALTER PROCEDURE dbo.spAlertaProductoPeligrosoPesoObtener
    @PesoMaximoInflamablesKg DECIMAL(8, 2),
    @PesoMaximoPeligrososKg DECIMAL(8, 2)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH ExistenciasPeligrosasSanBernardo AS
    (
        SELECT
             P.PRO_Id
            ,P.PRO_Codigo
            ,P.PRO_Nombre
            ,P.PRO_Peso
            ,P.PRO_EsInflamable
        FROM dbo.PRO_Producto P
        INNER JOIN dbo.XTN_Existencia X
            ON X.PRO_Id = P.PRO_Id
           AND X.XTN_Activo = 1
           AND X.XTN_Estado IN (0, 1, 2, 10, 11, 14)
        INNER JOIN dbo.ALM_Almacenamiento A
            ON A.ALM_Id = X.ALM_Id
           AND A.BOD_Id = 1 -- Bodega física San Bernardo
        WHERE P.PRO_Activo = 1
          AND EXISTS
          (
              SELECT 1
              FROM dbo.PROATR_ProductoAtributo PA
              WHERE PA.PRO_Id = P.PRO_Id
                AND PA.TATR_Id = 4039
          )
    ),
    PesosAcumulados AS
    (
        SELECT
             SUM(CASE WHEN PRO_EsInflamable = 1 THEN ISNULL(PRO_Peso, 0) ELSE 0 END) AS PesoInflamablesKg
            ,SUM(ISNULL(PRO_Peso, 0)) AS PesoPeligrososKg
        FROM ExistenciasPeligrosasSanBernardo
    )
    SELECT
         P.PRO_Id AS ProductoId
        ,P.PRO_Codigo AS Codigo
        ,P.PRO_Nombre AS Nombre
        ,P.PRO_Peso AS Peso
        ,P.PRO_EsInflamable AS EsInflamable
        ,CAST(NULL AS VARCHAR(50)) AS Tipo
        ,'SIN_PESO' AS TipoAnomalia
    FROM ExistenciasPeligrosasSanBernardo P
    WHERE ISNULL(P.PRO_Peso, 0) <= 0
    GROUP BY P.PRO_Id, P.PRO_Codigo, P.PRO_Nombre, P.PRO_Peso, P.PRO_EsInflamable

    UNION ALL

    SELECT
         0 AS ProductoId
        ,'INFLAMABLES' AS Codigo
        ,'Peso acumulado de productos inflamables en San Bernardo' AS Nombre
        ,PesoInflamablesKg AS Peso
        ,CAST(1 AS BIT) AS EsInflamable
        ,'Inflamables' AS Tipo
        ,'SOBRE_PESO' AS TipoAnomalia
    FROM PesosAcumulados
    WHERE PesoInflamablesKg > @PesoMaximoInflamablesKg

    UNION ALL

    SELECT
         0 AS ProductoId
        ,'PELIGROSOS' AS Codigo
        ,'Peso acumulado de productos peligrosos en San Bernardo' AS Nombre
        ,PesoPeligrososKg AS Peso
        ,CAST(NULL AS BIT) AS EsInflamable
        ,'Peligrosos' AS Tipo
        ,'SOBRE_PESO' AS TipoAnomalia
    FROM PesosAcumulados
    WHERE PesoPeligrososKg > @PesoMaximoPeligrososKg;
END;
