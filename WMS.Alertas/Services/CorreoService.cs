using System.Net;
using System.Net.Mail;
using System.Text;
using WMS.Alertas.Global;

namespace WMS.Alertas.Services;

public class CorreoService
{
    private readonly NodoEmailService _nodoEmailService;
    private readonly ConfiguracionEjecucionAlertas _configuracionEjecucion;

    public CorreoService(
        NodoEmailService nodoEmailService,
        ConfiguracionEjecucionAlertas configuracionEjecucion)
    {
        _nodoEmailService = nodoEmailService;
        _configuracionEjecucion = configuracionEjecucion;
    }

    public async Task EnviarCorreo(
     List<string> destinatarios,
     string asunto,
     string cuerpoHtml,
     byte[]? excelBytes = null,
     string? nombreArchivo = null)
    {
        if (!_configuracionEjecucion.Habilitadas)
        {
            throw new InvalidOperationException(
                "El envío de alertas está deshabilitado en este ambiente.");
        }

        var nodoEmail = await _nodoEmailService.ObtenerConfiguracion();

        if (nodoEmail == null)
            throw new Exception("No se encontró configuración de correo.");

        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls13;

        using var message = new MailMessage();

        message.From = new MailAddress(nodoEmail.Alias);

        var destinatariosFinales = destinatarios;
        var asuntoFinal = asunto;
        var cuerpoFinal = cuerpoHtml;

        if (!_configuracionEjecucion.EsProduccion)
        {
            if (_configuracionEjecucion.UsarCorreoPruebas)
            {
                var correoPruebas = _configuracionEjecucion.CorreoPruebas;

                if (string.IsNullOrWhiteSpace(correoPruebas))
                {
                    throw new InvalidOperationException(
                        "No existe un correo de pruebas configurado.");
                }

                destinatariosFinales = new List<string>
                {
                    correoPruebas
                };
            }

            asuntoFinal = $"[QA] {asunto}";
            var avisoDestino =
                _configuracionEjecucion.UsarCorreoPruebas
                    ? "Este mensaje fue redirigido al destinatario de pruebas y no se envió a usuarios operacionales."
                    : "Prueba controlada enviada a los destinatarios originales del proceso.";
            cuerpoFinal = $"""
                <div style="background-color:#fff3cd;border:1px solid #ffeeba;padding:15px;margin-bottom:20px;">
                    <strong>AMBIENTE QA - CORREO DE PRUEBA</strong><br/>
                    {avisoDestino}
                </div>
                """ + cuerpoHtml;
        }

        foreach (var destinatario in destinatariosFinales)
        {
            message.To.Add(destinatario);
        }

        message.Subject = asuntoFinal;
        message.SubjectEncoding = Encoding.UTF8;

        message.Body = cuerpoFinal;
        message.BodyEncoding = Encoding.UTF8;
        message.IsBodyHtml = true;

        if (excelBytes != null && excelBytes.Length > 0)
        {
            message.Attachments.Add(new Attachment(
                new MemoryStream(excelBytes),
                nombreArchivo ?? "AlertaWMS.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            ));
        }

        using var smtp = new SmtpClient();

        smtp.Host = nodoEmail.Host;
        smtp.Port = nodoEmail.Puerto;
        smtp.EnableSsl = nodoEmail.UsaSSL;
        smtp.UseDefaultCredentials = false;
        smtp.Credentials = new NetworkCredential(
            nodoEmail.Email,
            nodoEmail.EmailContrasena);

        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtp.Timeout = 60000;

        await smtp.SendMailAsync(message);
    }
}
