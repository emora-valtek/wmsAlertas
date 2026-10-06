# Base de datos

Toda creación o modificación de base de datos debe quedar en ambos lugares antes
de considerarse terminada:

1. `WMS.Alertas/Database/En curso/`, dentro de una carpeta identificable por el
   cambio, para que el paso a producción pueda tomar los scripts pendientes.
2. Su ubicación técnica permanente: `WMS.Alertas/Database/Procedimientos Alertas/`
   para procedimientos almacenados, o `WMS.Alertas/Database/Scripts/` para scripts
   de despliegue, datos de configuración y otras modificaciones de BD.

Si un cambio requiere procedimiento y configuración (por ejemplo, destinatarios),
el script de `En curso` debe instalar ambos en el orden necesario.

Los archivos que contienen procedimientos almacenados deben llamarse exactamente
igual que el procedimiento que definen, incluido el prefijo `sp` y con extensión
`.sql` (por ejemplo, `spAlertaProductoPeligrosoPesoObtener.sql`).

Al modificar un procedimiento almacenado existente, se deben conservar todos sus
comentarios históricos. Se debe agregar al bloque de comentarios una única entrada
por proyecto o sesión con el formato `EMORA dd-MM-yyyy descripción del cambio`;
si durante la misma sesión se realizan más modificaciones al mismo procedimiento,
esa única entrada debe actualizarse para resumirlas, sin agregar otra.
