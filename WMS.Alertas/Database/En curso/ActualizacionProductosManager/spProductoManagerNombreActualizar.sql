/*
    EMORA 16-10-2026 Actualiza exclusivamente el nombre de un producto sincronizado desde Manager.
*/
CREATE OR ALTER PROCEDURE dbo.spProductoManagerNombreActualizar
    @Codigo VARCHAR(30),
    @Nombre VARCHAR(150),
    @ModificadoPor INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Codigo = LTRIM(RTRIM(@Codigo));
    SET @Nombre = LTRIM(RTRIM(@Nombre));

    IF NULLIF(@Codigo, '') IS NULL
        THROW 50001, 'Debe indicar el código del producto de Manager.', 1;
    IF NULLIF(@Nombre, '') IS NULL
        THROW 50002, 'Debe indicar el nombre del producto de Manager.', 1;

    UPDATE dbo.PRO_Producto
    SET PRO_Nombre = @Nombre,
        PRO_ModificadoPor = @ModificadoPor,
        PRO_FechaModificacion = GETDATE()
    WHERE UPPER(LTRIM(RTRIM(PRO_Codigo))) = UPPER(@Codigo)
      AND CONVERT(VARBINARY(300), LTRIM(RTRIM(PRO_Nombre)))
          <> CONVERT(VARBINARY(300), @Nombre);

    SELECT CAST(IIF(@@ROWCOUNT = 1, 1, 0) AS BIT) AS NombreActualizado;
END;
GO
