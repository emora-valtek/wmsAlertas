# WMS.Alertas

Servicio .NET 8 que ejecuta alertas operacionales del WMS mediante Hangfire,
consulta datos en SQL Server y distribuye correos utilizando la configuración
SMTP almacenada en la base de datos.

## Alertas y horarios

Todos los horarios usan la zona `Pacific SA Standard Time` (Chile).

| Job | Horario | Descripción |
| --- | --- | --- |
| Pendientes de ingreso - producción propia | Lunes a viernes, 08:30 | Informa existencias de producción propia pendientes de ingreso. |
| Pendientes de ingreso - mercadería | Lunes a viernes, 08:35 | Informa mercadería pendiente de ingreso. |
| Pendientes de ingreso - consolidado | Lunes a viernes, 08:40 | Envía el consolidado de pendientes. |
| Stock asignado sin Packing List | Lunes a viernes, 09:00 | Informa stock asignado que todavía no tiene Packing List. |
| Lotes reservados con saldo mínimo | Lunes a viernes, 09:05 | Notifica al solicitante cuando una reserva alcanza el mínimo. |
| Respaldo Packing List AM | Lunes a viernes, 09:15 | Recupera alertas pendientes con más de 15 minutos. |
| Packing List pendientes | Lunes a viernes, 08:00 | Informa Packing List con más de dos días sin avanzar en recolección o embalaje. |
| Respaldo Packing List PM | Lunes a viernes, 15:00 | Segundo barrido de alertas pendientes. |

La migración del control de vencimientos desde `portal.Job` se implementa mediante
el proceso manual `ControlVencimientos_EnviarRevision` y el control consolidado
`Alerta_LoteReservados`. Antes de probarlos se debe ejecutar
los archivos `Database/En curso/20260904_ControlVencimientosDiagnostico.sql` y
`Database/En curso/20260904_spVencimientosRevisionar.sql` en `PortalDB`. Cada
archivo instala un procedimiento. El procesamiento
queda inicialmente sin horario automático.

## Ambientes y ejecución en QA

El ambiente se identifica mediante `ASPNETCORE_ENVIRONMENT`:

- `Development`: ejecución local en modo `Manual`, usando User Secrets para la
  conexión y las credenciales;
- `Production`: modo `Automatico`, con los horarios operacionales;
- `QA`: modo `Manual`, sin ejecuciones programadas.

En Development y QA los jobs se registran con `Cron.Never()` y aparecen en `/hangfire`, pero no
se ejecutan por horario. El encargado elige un job y utiliza `Trigger now` para
probarlo. La ejecución utiliza la cola exclusiva `qa-manual`, por lo que no toma
trabajos antiguos de la cola automática.

QA utiliza por defecto `"UsarCorreoPruebas": true`, que redirige todo al correo
definido en `appsettings.QA.json`. El correo puede sustituirse sin cambiar el
código mediante:

```text
Alertas__CorreoPruebas=correo_de_la_persona_que_prueba
```

Para una prueba controlada con los destinatarios originales se cambia en el
`appsettings.QA.json` publicado:

```json
"UsarCorreoPruebas": false
```

El valor normal y seguro es `true`. En ambos casos el asunto comienza con `[QA]`
y el cuerpo identifica el mensaje como prueba. Después de cambiar el valor se
debe reciclar el Application Pool. El endpoint automático de Packing List responde
sin encolar trabajo en modo manual. Para apagar incluso la ejecución manual puede
configurarse `Alertas__Modo=Deshabilitado`.

## Flujo de Packing List

El WMS registra primero el evento en `dbo.AlertaPackingList` y luego llama a:

```text
POST /api/alertas/packing-list/procesar-pendientes
```

En producción la petición encola el procesamiento. El job reserva filas de
manera atómica, agrupa eventos por Packing List y vendedor, envía el correo y
actualiza cada fila individualmente. En QA manual la petición no encola trabajo.

Si la llamada inmediata no ocurre, los respaldos de las 09:15 y 15:00 procesan
las filas que continúen pendientes por más de 15 minutos. Una reserva abandonada
por una caída se libera después de 60 minutos.

## Seguridad de la API

Los endpoints `/health` y `/api/alertas/packing-list/*` exigen el header:

```text
X-API-Key: <clave>
```

La clave se configura fuera de Git mediante `ApiSecurity__ApiKey`. Puede
generarse una clave de 48 bytes con PowerShell:

```powershell
$bytes = New-Object byte[] 48
$generador = [Security.Cryptography.RandomNumberGenerator]::Create()
$generador.GetBytes($bytes)
$generador.Dispose()
[Convert]::ToBase64String($bytes)
```

Si la clave no está configurada, los endpoints protegidos devuelven `503`; si
la clave recibida no coincide, devuelven `401`.

El backend del WMS está preparado para leer `WmsAlertas__ApiKey` y enviar este
header sin intervención del usuario. La misma clave debe configurarse en ambos
servicios antes de publicar. Nunca debe enviarse al navegador ni formar parte de
la interfaz del usuario.

## Configuración

Configuraciones requeridas:

| Clave | Uso |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | SQL Server usado por alertas y Hangfire. |
| `ApiSecurity__ApiKey` | Clave compartida para los endpoints internos. |
| `ASPNETCORE_ENVIRONMENT` | `Production` en producción y `QA` en el servidor de pruebas. |
| `Alertas__Modo` | `Automatico`, `Manual` o `Deshabilitado`; los archivos de ambiente ya definen el valor normal. |
| `Alertas__CorreoPruebas` | Override opcional del destinatario definido en `appsettings.QA.json`. |
| `Alertas__UsarCorreoPruebas` | `true` redirige al correo de pruebas; `false` utiliza los destinatarios originales. |

En desarrollo pueden utilizarse User Secrets. En producción deben configurarse
como variables de entorno del proceso o mediante la configuración protegida de
IIS. No se deben escribir secretos reales en `appsettings.json`.

Antes de publicar esta versión debe ejecutarse:

```text
WMS.Alertas/Database/20260819_MonitoreoYRespaldoAlertas.sql
```

El script:

- agrega la antigüedad opcional a la toma de pendientes;
- devuelve el estado final al registrar un error de Packing List;
- crea o actualiza el destinatario `ErrorWMSAlertas`.

Para cambiar de encargado solo se actualiza el registro activo de
`dbo.AlertasCorreoDestino` con `TipoAlerta = 'ErrorWMSAlertas'`; no se necesita
volver a publicar el servicio.

## Notificación de fallos

Los jobs que terminan en estado `Failed` después de los reintentos de Hangfire
envían un correo al encargado. El correo incluye proceso, fecha, servidor, ID de
Hangfire y excepción.

Packing List maneja reintentos por fila en la base de datos. El encargado recibe
un aviso solamente cuando una fila alcanza el máximo de cinco intentos.

La notificación usa la misma base de datos y SMTP del servicio. Por ese motivo
no puede informar una caída completa de la aplicación, del servidor o de la base
de datos; ese caso lo cubre el monitor externo.

## Health check y monitor externo

`GET /health` devuelve únicamente:

- `200 { "estado": "OK" }` cuando la base de datos responde, Hangfire tiene un
  heartbeat reciente y los jobs diarios esperados registraron actividad;
- `503 { "estado": "ERROR" }` cuando alguna comprobación falla.

En modo manual, el health check exige aplicación, base de datos y heartbeat de
Hangfire, pero no exige ejecuciones diarias. En modo deshabilitado solo comprueba
la aplicación y la base de datos.

Antes de las 09:15 no se exige actividad diaria. Los fines de semana tampoco se
exigen ejecuciones, aunque se siguen comprobando aplicación, base de datos y
Hangfire.

El script `WMS.Alertas/Monitoring/Monitor-WMSAlertas.ps1` debe copiarse a otro
servidor y programarse de lunes a viernes a las 09:30. Realiza tres intentos y
solo envía correo cuando el health check no responde correctamente.

Variables requeridas por el monitor:

```text
WMS_ALERTAS_API_KEY
WMS_ALERTAS_MONITOR_SMTP_HOST
WMS_ALERTAS_MONITOR_SMTP_PORT
WMS_ALERTAS_MONITOR_SMTP_USUARIO
WMS_ALERTAS_MONITOR_SMTP_CLAVE
WMS_ALERTAS_MONITOR_REMITENTE
WMS_ALERTAS_MONITOR_DESTINATARIO
```

Ejemplo de acción en el Programador de tareas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Monitoreo\Monitor-WMSAlertas.ps1" -Url "https://servidor-alertas/health"
```

El monitor no debe instalarse en el mismo servidor de WMS.Alertas si se quiere
detectar también la caída completa de ese servidor.

## Dashboard de Hangfire

El dashboard está en `/hangfire`. Se conserva la autorización predeterminada de
Hangfire, que acepta solamente solicitudes locales. El encargado debe abrirlo
desde un navegador dentro del servidor.

En QA, los jobs recurrentes muestran programación `Never`. Para ejecutar una
prueba se selecciona exclusivamente el job requerido y se utiliza `Trigger now`.

No debe publicarse a la red. Si en el futuro se necesita acceso remoto, se debe
habilitar autenticación Windows en IIS y autorizar únicamente al grupo de
encargados.

## Publicación

Orden recomendado:

1. Respaldar la base de datos.
2. Ejecutar el script SQL de esta versión.
3. Configurar `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`
   y `ApiSecurity__ApiKey`.
4. Configurar la misma clave como `WmsAlertas__ApiKey` en el backend del WMS.
5. Publicar en Release con el perfil `FolderProfile`.
6. Confirmar `/health` y los dos endpoints con `WMS.Alertas.http`.
7. Revisar los jobs recurrentes en `/hangfire` desde el servidor.
8. Instalar el monitor externo en el servidor elegido.

Compilación local:

```powershell
dotnet build WMS.Alertas.sln -c Release
```

Publicación mediante CLI:

```powershell
dotnet publish WMS.Alertas/WMS.Alertas.csproj -c Release -o C:\publicaciones\WMS.Alertas
```

## Diagnóstico

- `dbo.AlertasEjecucionLog`: inicio, término, estado y destinatarios de los jobs.
- `dbo.AlertaPackingList`: estado e intentos de cada alerta de Packing List.
- `/hangfire`: jobs encolados, reintentos y fallos definitivos.
- `/health`: estado resumido para monitoreo automático.

No deben enviarse manualmente correos ni cambiarse estados de la cola sin
revisar antes los intentos y el identificador de reserva (`ProcesadoPor`).
