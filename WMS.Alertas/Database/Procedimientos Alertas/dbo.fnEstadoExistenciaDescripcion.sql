
/*
    EMORA 28-04-2026 Se crea función que devuelve el estado de una existencia según el número
    EMORA 22-10-2026 Estado 20 pasa a "Faltante de inventario"; se quitan los estados que ya no se usan
*/

CREATE OR ALTER FUNCTION dbo.fnEstadoExistenciaDescripcion
(
    @Estado INT
)
RETURNS VARCHAR(100)
AS
BEGIN
    RETURN
    (
        CASE @Estado
            WHEN 0 THEN 'Recepcionado'
            WHEN 1 THEN 'Pendiente de Ingreso'
            WHEN 2 THEN 'Disponible'
            WHEN 8 THEN 'Embalado'
            WHEN 9 THEN 'Merma'
            WHEN 10 THEN 'Vencimiento'
            WHEN 11 THEN 'En revisión'
            WHEN 12 THEN 'Despachado'
            WHEN 14 THEN 'Consumiendo'
            WHEN 15 THEN 'Consumido'
            WHEN 18 THEN 'Despachado por Tracking'
            WHEN 19 THEN 'Descartado Producción Propia'
            WHEN 20 THEN 'Faltante de inventario'
            WHEN 21 THEN 'Cancelado'
            ELSE 'Desconocido'
        END
    );
END
