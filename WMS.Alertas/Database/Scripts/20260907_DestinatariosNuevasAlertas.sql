/*
   EMORA 07-09-2026 Destinatarios iniciales de las nuevas alertas WMS
*/

DECLARE @Destinatarios TABLE (TipoAlerta VARCHAR(100), Nombre VARCHAR(200), Email VARCHAR(320));
INSERT @Destinatarios (TipoAlerta, Nombre, Email)
VALUES
    ('ExistenciasRevision', 'Felipe Vargas', 'fvargas@valtek.cl'),
    ('ExistenciasRevision', 'Jorge Hernández', 'jhernandez@valtek.cl'),
    ('ExistenciasRevision', 'Sebastián García Lizama', 'sgarcializama@valtek.cl'),
    ('ExistenciasRevision', 'Logística', 'logistica@valtek.cl'),
    ('LoteReservados', 'Felipe Vargas', 'fvargas@valtek.cl'),
    ('LoteReservados', 'Jorge Hernández', 'jhernandez@valtek.cl'),
    ('LoteReservados', 'Sebastián García Lizama', 'sgarcializama@valtek.cl'),
    ('LoteReservados', 'Víctor Martínez', 'vmartinez@valtek.cl'),
    ('LoteReservados', 'Logística', 'logistica@valtek.cl'),
    ('PackingListPendientes', 'Encargado WMS', 'emora@valtek.cl');

UPDATE dbo.AlertasCorreoDestino
SET Activo = 0
WHERE TipoAlerta IN ('ExistenciasRevision', 'LoteReservados');

UPDATE D
SET D.Nombre = N.Nombre,
    D.Email = N.Email,
    D.Activo = 1
FROM dbo.AlertasCorreoDestino D
INNER JOIN @Destinatarios N
    ON N.TipoAlerta = D.TipoAlerta
   AND N.Email = D.Email;

INSERT dbo.AlertasCorreoDestino (TipoAlerta, Nombre, Email, Activo)
SELECT N.TipoAlerta, N.Nombre, N.Email, 1
FROM @Destinatarios N
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.AlertasCorreoDestino D
    WHERE D.TipoAlerta = N.TipoAlerta
);
GO
