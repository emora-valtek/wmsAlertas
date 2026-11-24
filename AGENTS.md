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

Si un script de `Scripts/` crea o modifica un procedimiento, el mismo cuerpo debe quedar también en
`Procedimientos/dbo.<Nombre>.sql`. Antes de modificar un procedimiento existente, partir de su definición
en **producción**, no del archivo del repositorio. (El 24-11-2026 se detectó que
`spAlertaPackingListPendientesTomar` se había actualizado desde
`Scripts/20260819_MonitoreoYRespaldoAlertas.sql` y el archivo de `Procedimientos/` seguía sin
`@MinutosAntiguedad`.)

Al modificar un procedimiento almacenado existente, se deben conservar todos sus
comentarios históricos y agregar una única línea `EMORA dd-mm-aaaa descripción del cambio`
por paso a producción; si el mismo cambio se ajusta varias veces antes de desplegarse,
esa línea se reescribe para resumirlo, sin agregar otra.

La lista de procedimientos que invoca el servicio está en `README.md`, sección
"Base de datos". Al agregar una llamada nueva desde el código, actualizar esa lista.

# Avisos de Packing List: tipos que procesa `PackingListModificadoJob`

`CAMBIO_LOTE`, `BAJA_CANTIDAD`, `DEVUELTO_A_SAC` y, desde el 24-11-2026, `PL_CANCELADO` (toda cancelación de PL desde
bodega) y `ASIGNACION_CANCELADA` (stock asignado a la NV liberado al enviar a revisión, sin PL: `PackingListId` NULL y
se agrupa por `DocumentoEgresoId`). Detalle y textos acordados en
`PortalValtek/Portal.DB/_EnCurso/20261120_LiberarRevision/README.md`, sección «Aviso a SAC».

- El destinatario es siempre quien creó la NV (`CorreoVendedor`). El número de NV a mostrar es `NumeroNotaVenta`.
- Los correos de los dos tipos nuevos no muestran la `Observacion` (trae el motivo de revisión y el supervisor).
- `spAlertaPackingListPendientesTomar` entrega solo los tipos de esta lista. Para agregar uno nuevo, primero se enseña
  al job y después se agrega al SP; si no, el aviso queda PENDIENTE.
- Publicar WMS.Alertas **antes** que la base cuando un cambio del SP entregue datos que la versión anterior no sabe leer.
