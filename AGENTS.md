# Base de datos

Este repositorio no contiene objetos de base de datos. WMS.Alertas usa la misma base
`PortalDB` que el WMS, y todos sus procedimientos, funciones y scripts se mantienen en
el repositorio `PortalValtek`, carpeta `Portal.DB` (`C:\GIT\GITportalvaltek\PortalValtek\Portal.DB`).

Toda creación o modificación de base de datos que necesite este servicio se hace allí,
con las reglas de ese repositorio:

1. `Portal.DB/_EnCurso/<aaaammdd>_<Cambio>/`, con un `README.md` de pasos, para que el
   paso a producción pueda tomar lo pendiente.
2. Su ubicación permanente: `Portal.DB/Procedimientos/dbo.<Nombre>.sql` para
   procedimientos, `Portal.DB/Funciones/` para funciones y `Portal.DB/Scripts/` para
   scripts de despliegue, datos de configuración y otras modificaciones.

Si un cambio requiere procedimiento y configuración (por ejemplo, destinatarios), el
script de `_EnCurso` debe instalar ambos en el orden necesario.

Al modificar un procedimiento almacenado existente, se deben conservar todos sus
comentarios históricos y agregar una única línea `EMORA dd-mm-aaaa descripción del cambio`
por paso a producción; si el mismo cambio se ajusta varias veces antes de desplegarse,
esa línea se reescribe para resumirlo, sin agregar otra.

La lista de procedimientos que invoca el servicio está en `README.md`, sección
"Base de datos". Al agregar una llamada nueva desde el código, actualizar esa lista.
