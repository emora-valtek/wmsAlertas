/*
    EMORA 06-10-2026 Actualiza pesos, atributos Peligroso/Ambiente y la marca
    de inflamable según el libro "Productos Peligrosos Logística. HDS y Simbologia".

    Validado contra producción el 06-10-2026. Solo modifica registros que difieren.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @UsuarioId INT = 10001;
DECLARE @PesosActualizados INT = 0;
DECLARE @InflamablesActualizados INT = 0;
DECLARE @AmbienteReactivados INT = 0;
DECLARE @AmbienteCreados INT = 0;
DECLARE @PeligrososEliminados INT = 0;
DECLARE @AmbientesEliminados INT = 0;

DECLARE @Pesos TABLE
(
    Codigo VARCHAR(50) NOT NULL PRIMARY KEY,
    Peso DECIMAL(8, 2) NOT NULL
);

INSERT @Pesos (Codigo, Peso)
VALUES
    ('16128', 1),
    ('1000-9546301', 1.4288),
    ('1013830500', 0.55),
    ('1013832500', 2.55),
    ('10385229', 0.2),
    ('10385231', 0.5198),
    ('1046990100', 0.27181),
    ('1046990500', 0.987),
    ('105-000107-00', 2.1),
    ('105-001688-00', 2.1),
    ('105-001689-00', 0.279),
    ('105-001690-00', 0.27976),
    ('105-001692-00', 0.28792),
    ('105-009143-00', 0.26181),
    ('105-012294-00', 0.14264),
    ('105-012296-00', 0.14256),
    ('105-012298-00', 0.14333),
    ('105-048453-A0', 2.1),
    ('105-048464-A0', 0.694),
    ('1060092500', 3.0002),
    ('1119562500', 2.6),
    ('130299004M', 0.56),
    ('130299007M', 0.73718),
    ('225-LPWS2', 0.27407),
    ('240-154', 0.604),
    ('240-160', 0.60715),
    ('240-198', 0.12564),
    ('240-208', 0.08976),
    ('240-214', 0.1854),
    ('250-110', 1),
    ('260-100', 0.21945),
    ('260-110', 0.26),
    ('260-120', 0.27142),
    ('260-140', 0.27775),
    ('260-170', 0.26),
    ('290-605', 0.11),
    ('317-800', 0.279),
    ('317-810', 0.282),
    ('317-820', 0.143),
    ('317-830', 0.2788),
    ('367587', 0.61016),
    ('367812', 0.606),
    ('367841', 0.6062),
    ('367856', 0.6064),
    ('367861', 0.607),
    ('367863', 0.608),
    ('367922', 0.611),
    ('6000-0016-0526-0', 0.27415),
    ('6000-0016-2505-0', 0.231),
    ('620485V', 0.44126),
    ('620487V', 0.5663),
    ('AG792864', 0.1416),
    ('B1010-41A', 0.0988),
    ('B1010-42A', 0.07126),
    ('B1010-43A', 0.11),
    ('B1010-44A', 0.103),
    ('B1010-45A', 0.10217),
    ('B1010-48A', 0.10295),
    ('B1015-3', 0.10012),
    ('BA34-10-63645', 0.18828),
    ('CE919553', 0.14065),
    ('CL11014241022-1', 1.1),
    ('CL11092041022-1', 1.083),
    ('CX-110012V', 1.606),
    ('DPALCDE7', 1),
    ('K-4471', 3),
    ('P1060092511', 2.6),
    ('Q2260', 1),
    ('1013831022', 1);

DECLARE @Inflamables TABLE
(
    Codigo VARCHAR(50) NOT NULL PRIMARY KEY,
    EsInflamable BIT NOT NULL
);

INSERT @Inflamables (Codigo, EsInflamable)
VALUES
    ('1013830500', 1),
    ('1013831022', 1),
    ('250-110', 1),
    ('DPALCDE7', 1),
    ('K-4471', 1),
    ('130299007M', 0),
    ('260-300', 0),
    ('6000-0016-0526-0', 0);

DECLARE @ProductosAmbiente TABLE (Codigo VARCHAR(50) NOT NULL PRIMARY KEY);

INSERT @ProductosAmbiente (Codigo)
VALUES
    ('10308995'),
    ('105-000141-00'),
    ('105-004552-00'),
    ('115-18430'),
    ('240-162'),
    ('260-300'),
    ('317-512'),
    ('B1010-40'),
    ('B1010-40A'),
    ('B1013-12A'),
    ('H-10'),
    ('H-10-800'),
    ('O001');

DECLARE @ProductosPeligroso TABLE (Codigo VARCHAR(50) NOT NULL PRIMARY KEY);

INSERT @ProductosPeligroso (Codigo)
VALUES
    ('10309925'),
    ('10309926'),
    ('10309927'),
    ('10310032'),
    ('10310310'),
    ('10313526'),
    ('10327568'),
    ('10327572'),
    ('105-002232-00'),
    ('105-009143-A0'),
    ('10634644'),
    ('10718483'),
    ('10718484'),
    ('11379208191'),
    ('115-045559-00'),
    ('115-19552'),
    ('11662970122'),
    ('11662988122'),
    ('11706799001'),
    ('12010'),
    ('1300036421'),
    ('148600'),
    ('225-LKPM'),
    ('240-149'),
    ('261187'),
    ('290-510'),
    ('3110451180'),
    ('3112284180'),
    ('3112349180'),
    ('317-522'),
    ('362788'),
    ('365963'),
    ('365978'),
    ('367364'),
    ('401320'),
    ('410011-100'),
    ('442023'),
    ('442555'),
    ('443806'),
    ('443878'),
    ('621220V'),
    ('6527'),
    ('B1027-201'),
    ('DPALCDE7'),
    ('Q2260');

IF EXISTS
(
    SELECT 1
    FROM @Pesos D
    LEFT JOIN dbo.PRO_Producto P ON P.PRO_Codigo = D.Codigo
    WHERE P.PRO_Id IS NULL
)
    THROW 50001, 'Existen códigos de peso que no están creados en PRO_Producto.', 1;

IF EXISTS
(
    SELECT 1
    FROM @Inflamables D
    LEFT JOIN dbo.PRO_Producto P ON P.PRO_Codigo = D.Codigo
    WHERE P.PRO_Id IS NULL
)
    THROW 50002, 'Existen códigos de inflamabilidad que no están creados en PRO_Producto.', 1;

IF EXISTS
(
    SELECT 1
    FROM @ProductosAmbiente D
    LEFT JOIN dbo.PRO_Producto P ON P.PRO_Codigo = D.Codigo
    WHERE P.PRO_Id IS NULL
)
    THROW 50003, 'Existen códigos de atributo Ambiente que no están creados en PRO_Producto.', 1;

IF EXISTS
(
    SELECT 1
    FROM @ProductosPeligroso D
    LEFT JOIN dbo.PRO_Producto P ON P.PRO_Codigo = D.Codigo
    WHERE P.PRO_Id IS NULL
)
    THROW 50004, 'Existen códigos de atributo Peligroso que no están creados en PRO_Producto.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE P
    SET P.PRO_Peso = D.Peso,
        P.PRO_ModificadoPor = @UsuarioId,
        P.PRO_FechaModificacion = GETDATE()
    FROM dbo.PRO_Producto P
    INNER JOIN @Pesos D ON D.Codigo = P.PRO_Codigo
    WHERE P.PRO_Peso IS NULL
       OR ABS(P.PRO_Peso - D.Peso) > 0.00001;
    SET @PesosActualizados = @@ROWCOUNT;

    UPDATE P
    SET P.PRO_EsInflamable = D.EsInflamable,
        P.PRO_ModificadoPor = @UsuarioId,
        P.PRO_FechaModificacion = GETDATE()
    FROM dbo.PRO_Producto P
    INNER JOIN @Inflamables D ON D.Codigo = P.PRO_Codigo
    WHERE ISNULL(P.PRO_EsInflamable, 0) <> D.EsInflamable;
    SET @InflamablesActualizados = @@ROWCOUNT;

    UPDATE PA
    SET PA.PROATR_Activo = 1,
        PA.PROATR_ModificadoPor = @UsuarioId,
        PA.PROATR_FechaModificacion = GETDATE()
    FROM dbo.PROATR_ProductoAtributo PA
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = PA.PRO_Id
    INNER JOIN @ProductosAmbiente D ON D.Codigo = P.PRO_Codigo
    WHERE PA.TATR_Id = 4038
      AND PA.PROATR_Activo = 0;
    SET @AmbienteReactivados = @@ROWCOUNT;

    INSERT dbo.PROATR_ProductoAtributo
    (
        PRO_Id,
        TATR_Id,
        PROATR_ValorAtributo,
        PROATR_CreadoPor,
        PROATR_FechaCreacion,
        PROATR_Activo
    )
    SELECT
         P.PRO_Id,
        4038,
        '',
        @UsuarioId,
        GETDATE(),
        1
    FROM dbo.PRO_Producto P
    INNER JOIN @ProductosAmbiente D ON D.Codigo = P.PRO_Codigo
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.PROATR_ProductoAtributo PA
        WHERE PA.PRO_Id = P.PRO_Id
          AND PA.TATR_Id = 4038
    );
    SET @AmbienteCreados = @@ROWCOUNT;

    DELETE PA
    FROM dbo.PROATR_ProductoAtributo PA
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = PA.PRO_Id
    INNER JOIN @ProductosAmbiente D ON D.Codigo = P.PRO_Codigo
    WHERE PA.TATR_Id = 4039;
    SET @PeligrososEliminados = @@ROWCOUNT;

    DELETE PA
    FROM dbo.PROATR_ProductoAtributo PA
    INNER JOIN dbo.PRO_Producto P ON P.PRO_Id = PA.PRO_Id
    INNER JOIN @ProductosPeligroso D ON D.Codigo = P.PRO_Codigo
    WHERE PA.TATR_Id = 4038;
    SET @AmbientesEliminados = @@ROWCOUNT;

    COMMIT TRANSACTION;

    SELECT
         @PesosActualizados AS PesosActualizados,
         @InflamablesActualizados AS InflamablesActualizados,
         @AmbienteReactivados AS AmbienteReactivados,
         @AmbienteCreados AS AmbienteCreados,
         @PeligrososEliminados AS PeligrososEliminados,
         @AmbientesEliminados AS AmbientesEliminados;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
