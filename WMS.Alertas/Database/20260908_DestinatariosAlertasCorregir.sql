/*
   EMORA 08-09-2026 Corrige destinatarios de las nuevas alertas WMS
*/

DECLARE @Destinatarios TABLE
(
    TipoAlerta VARCHAR(100),
    Nombre VARCHAR(200),
    Email VARCHAR(320)
);

INSERT @Destinatarios (TipoAlerta, Nombre, Email)
VALUES
    ('ExistenciasRevision', 'Juan Carlos Hernández', 'jhernandez@valtek.cl'),
    ('ExistenciasRevision', 'Felipe Vargas', 'fvargas@valtek.cl'),
    ('ExistenciasRevision', 'Sergio García Lizama', 'sgarcializama@valtek.cl'),
    ('ExistenciasRevision', 'Logística', 'logistica@valtek.cl'),
    ('LoteReservados', 'Juan Carlos Hernández', 'jhernandez@valtek.cl'),
    ('LoteReservados', 'Felipe Vargas', 'fvargas@valtek.cl'),
    ('LoteReservados', 'Sergio García Lizama', 'sgarcializama@valtek.cl'),
    ('LoteReservados', 'Logística', 'logistica@valtek.cl'),
    ('PackingListPendientes', 'Juan Carlos Hernández', 'jhernandez@valtek.cl'),
    ('PackingListPendientes', 'Felipe Vargas', 'fvargas@valtek.cl'),
    ('PackingListPendientes', 'Víctor Martínez', 'vmartinez@valtek.cl'),
    ('PackingListPendientes', 'Logística', 'logistica@valtek.cl');

UPDATE dbo.AlertasCorreoDestino
SET Activo = 0
WHERE TipoAlerta IN
(
    'ExistenciasRevision',
    'LoteReservados',
    'PackingListPendientes'
);

UPDATE D
SET D.Nombre = N.Nombre,
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
      AND D.Email = N.Email
);
GO

delete from AlertasCorreoDestino where Activo = 0
go
