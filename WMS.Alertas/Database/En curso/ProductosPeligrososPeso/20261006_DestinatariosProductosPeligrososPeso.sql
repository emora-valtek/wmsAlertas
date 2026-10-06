/*
    Destinatarios de la alerta diaria de anomalías de peso en productos peligrosos.
*/
DECLARE @Destinatarios TABLE (Nombre VARCHAR(200), Email VARCHAR(320));
INSERT @Destinatarios (Nombre, Email)
VALUES
    ('Adquisiciones', 'adquisiciones@valtek.cl'),
    ('Claudia Acevedo', 'cacevedo@valtek.cl'),
    ('Jorge Hernández', 'jhernandez@valtek.cl');

UPDATE dbo.AlertasCorreoDestino
SET Activo = 0
WHERE TipoAlerta = 'ProductosPeligrososPeso';

UPDATE D
SET D.Nombre = N.Nombre,
    D.Activo = 1
FROM dbo.AlertasCorreoDestino D
INNER JOIN @Destinatarios N
    ON N.Email = D.Email
WHERE D.TipoAlerta = 'ProductosPeligrososPeso';

INSERT dbo.AlertasCorreoDestino (TipoAlerta, Nombre, Email, Activo)
SELECT 'ProductosPeligrososPeso', N.Nombre, N.Email, 1
FROM @Destinatarios N
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.AlertasCorreoDestino D
    WHERE D.TipoAlerta = 'ProductosPeligrososPeso'
      AND D.Email = N.Email
);
GO
