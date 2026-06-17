using System.Net;
using System.Net.Mail;
using System.Text;

namespace WMS.Alertas.Services;

public class CorreoService
{
    private readonly NodoEmailService _nodoEmailService;

    public CorreoService(NodoEmailService nodoEmailService)
    {
        _nodoEmailService = nodoEmailService;
    }

    public async Task EnviarCorreo(
     List<string> destinatarios,
     string asunto,
     string cuerpoHtml,
     byte[]? excelBytes = null,
     string? nombreArchivo = null)
    {
        var nodoEmail = await _nodoEmailService.ObtenerConfiguracion();

        if (nodoEmail == null)
            throw new Exception("No se encontró configuración de correo.");

        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls13;

        using var message = new MailMessage();

        message.From = new MailAddress(nodoEmail.Alias);

        foreach (var destinatario in destinatarios)
        {
            message.To.Add(destinatario);
        }

        message.Subject = asunto;
        message.SubjectEncoding = Encoding.UTF8;

        message.Body = cuerpoHtml;
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

    //pruebas para desarrollo
    public async Task EnviarCorreoPrueba(
    List<string> destinatarios,
    string asunto,
    string cuerpoHtml,
    byte[]? excelBytes = null,
    string? nombreArchivo = null)
    {
        var nodoEmail = await _nodoEmailService.ObtenerConfiguracion();

        if (nodoEmail == null)
            throw new Exception("No se encontró configuración de correo.");

        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls13;

        using var message = new MailMessage();

        message.From = new MailAddress(nodoEmail.Alias);

        //foreach (var destinatario in destinatarios)
        //{
        //    message.To.Add(destinatario);
        //}
        message.To.Add("emora@valtek.cl");
        //message.To.Add("molmedo@valtek.cl");

        message.Subject = asunto;
        message.SubjectEncoding = Encoding.UTF8;

        cuerpoHtml = @"
        <div style='background-color:#fff3cd;
            border:1px solid #ffeeba;
            padding:15px;
            margin-bottom:20px;'>

            <strong>⚠️ ESTO ES UNA PRUEBA</strong><br/>
            Correo generado para validar el envío de alertas automáticas del WMS.<br/>
            Los datos contenidos en este correo son de prueba y no deben ser considerados para gestión operativa.

        </div>"
        + cuerpoHtml;

        message.Body = cuerpoHtml;
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