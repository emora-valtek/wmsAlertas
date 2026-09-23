/*
    Agrega a José Albahaca como destinatario de la alerta de Packing List pendientes.
    La validación previa evita crear registros duplicados al ejecutar el script nuevamente.
*/
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.AlertasCorreoDestino
    WHERE TipoAlerta = 'PackingListPendientes'
      AND LOWER(Email) = 'logisticainversa@valtek.cl'
)
BEGIN
    INSERT INTO dbo.AlertasCorreoDestino
    (
        TipoAlerta,
        Nombre,
        Email,
        Activo,
        FechaCreacion
    )
    VALUES
    (
        'PackingListPendientes',
        'José Albahaca',
        'logisticainversa@valtek.cl',
        1,
        GETDATE()
    );

    PRINT 'Destinatario agregado a PackingListPendientes.';
END
ELSE
BEGIN
    PRINT 'El destinatario ya existe para PackingListPendientes.';
END
GO
