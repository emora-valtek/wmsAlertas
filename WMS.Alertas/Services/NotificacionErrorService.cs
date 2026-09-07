using System.Net;
using System.Text;
using WMS.Alertas.Global;

namespace WMS.Alertas.Services;

public sealed class NotificacionErrorService
{
    private readonly CorreoDestinoService _correoDestinoService;
    private readonly CorreoService _correoService;
    private readonly ILogger<NotificacionErrorService> _logger;
    private readonly ConfiguracionEjecucionAlertas _configuracionEjecucion;

    public NotificacionErrorService(
        CorreoDestinoService correoDestinoService,
        CorreoService correoService,
        ILogger<NotificacionErrorService> logger,
        ConfiguracionEjecucionAlertas configuracionEjecucion)
    {
        _correoDestinoService = correoDestinoService;
        _correoService = correoService;
        _logger = logger;
        _configuracionEjecucion = configuracionEjecucion;
    }

    public async Task Notificar(
        string proceso,
        string detalle,
        string? jobId = null)
    {
        var destinatarios = await _correoDestinoService.ObtenerCorreos(
            ConfiguracionAlertas.TipoDestinatarioErrores);

        if (destinatarios.Count == 0)
        {
            if (!_configuracionEjecucion.EsProduccion &&
                _configuracionEjecucion.UsarCorreoPruebas &&
                !string.IsNullOrWhiteSpace(_configuracionEjecucion.CorreoPruebas))
            {
                destinatarios.Add(_configuracionEjecucion.CorreoPruebas);
            }
            else
            {
                _logger.LogError(
                    "No hay destinatario activo del tipo {Tipo} para informar el fallo de {Proceso}.",
                    ConfiguracionAlertas.TipoDestinatarioErrores,
                    proceso);
                return;
            }
        }

        var servidor = Environment.MachineName;
        var fechaChile = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            ConfiguracionAlertas.ZonaHorariaChile);
        var html = new StringBuilder()
            .AppendLine("<h2>WMS.Alertas - Error de procesamiento</h2>")
            .AppendLine($"<p><strong>Proceso:</strong> {WebUtility.HtmlEncode(proceso)}</p>")
            .AppendLine($"<p><strong>Fecha:</strong> {fechaChile:dd-MM-yyyy HH:mm:ss}</p>")
            .AppendLine($"<p><strong>Servidor:</strong> {WebUtility.HtmlEncode(servidor)}</p>");

        if (!string.IsNullOrWhiteSpace(jobId))
        {
            html.AppendLine(
                $"<p><strong>Job Hangfire:</strong> {WebUtility.HtmlEncode(jobId)}</p>");
        }

        html.AppendLine("<h3>Detalle</h3>")
            .AppendLine(
                $"<pre style='white-space:pre-wrap'>{WebUtility.HtmlEncode(detalle)}</pre>");

        await _correoService.EnviarCorreo(
            destinatarios,
            $"Error WMS.Alertas - {proceso}",
            html.ToString());
    }
}
