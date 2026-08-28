[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [string]$ApiKey = $env:WMS_ALERTAS_API_KEY,
    [int]$Intentos = 3,
    [int]$EsperaSegundos = 30
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw 'Falta la variable WMS_ALERTAS_API_KEY.'
}

$ultimoError = $null

for ($intento = 1; $intento -le $Intentos; $intento++) {
    try {
        $respuesta = Invoke-RestMethod `
            -Uri $Url `
            -Method Get `
            -Headers @{ 'X-API-Key' = $ApiKey } `
            -TimeoutSec 30

        if ($respuesta.Estado -eq 'OK') {
            exit 0
        }

        $ultimoError = "Respuesta inesperada del health check: $($respuesta | ConvertTo-Json -Compress)"
    }
    catch {
        $ultimoError = $_.Exception.Message
    }

    if ($intento -lt $Intentos) {
        Start-Sleep -Seconds $EsperaSegundos
    }
}

$smtpHost = $env:WMS_ALERTAS_MONITOR_SMTP_HOST
$smtpPort = $env:WMS_ALERTAS_MONITOR_SMTP_PORT
$smtpUsuario = $env:WMS_ALERTAS_MONITOR_SMTP_USUARIO
$smtpClave = $env:WMS_ALERTAS_MONITOR_SMTP_CLAVE
$remitente = $env:WMS_ALERTAS_MONITOR_REMITENTE
$destinatario = $env:WMS_ALERTAS_MONITOR_DESTINATARIO

$faltantes = @()
foreach ($variable in @(
    'WMS_ALERTAS_MONITOR_SMTP_HOST',
    'WMS_ALERTAS_MONITOR_SMTP_PORT',
    'WMS_ALERTAS_MONITOR_SMTP_USUARIO',
    'WMS_ALERTAS_MONITOR_SMTP_CLAVE',
    'WMS_ALERTAS_MONITOR_REMITENTE',
    'WMS_ALERTAS_MONITOR_DESTINATARIO'
)) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variable))) {
        $faltantes += $variable
    }
}

if ($faltantes.Count -gt 0) {
    throw "WMS.Alertas no respondió y no se pudo enviar el aviso. Faltan: $($faltantes -join ', '). Error: $ultimoError"
}

$mensaje = [System.Net.Mail.MailMessage]::new()
$smtp = [System.Net.Mail.SmtpClient]::new($smtpHost, [int]$smtpPort)

try {
    $mensaje.From = [System.Net.Mail.MailAddress]::new($remitente)
    $mensaje.To.Add($destinatario)
    $mensaje.Subject = 'WMS.Alertas no está respondiendo correctamente'
    $mensaje.Body = @"
El monitor externo no pudo validar WMS.Alertas después de $Intentos intentos.

URL: $Url
Servidor monitor: $env:COMPUTERNAME
Fecha: $(Get-Date -Format 'dd-MM-yyyy HH:mm:ss')
Error: $ultimoError
"@

    $smtp.EnableSsl = $true
    $smtp.UseDefaultCredentials = $false
    $smtp.Credentials = [System.Net.NetworkCredential]::new(
        $smtpUsuario,
        $smtpClave)
    $smtp.Send($mensaje)
}
finally {
    $mensaje.Dispose()
    $smtp.Dispose()
}

exit 1
