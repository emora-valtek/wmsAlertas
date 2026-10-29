using System.Text;
using System.Xml.Linq;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

/// <summary>Cliente mínimo del servicio SOAP ConsultaProd de Manager.</summary>
public sealed class ManagerProductosClient
{
    private const string SoapNamespace = "http://manager.cl/productos/";
    private const int MaximoIntentosConexion = 3;
    private static readonly TimeSpan PausaEntreIntentos = TimeSpan.FromMinutes(1);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ManagerProductosClient> _logger;

    public ManagerProductosClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ManagerProductosClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductoManager>> ObtenerProductos(CancellationToken cancellationToken)
    {
        var endpoint = _configuration["ManagerProductos:Endpoint"];
        var rutEmpresa = _configuration["ManagerProductos:RutEmpresa"];
        var token = _configuration["ManagerProductos:Token"];

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(rutEmpresa) ||
            string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Debe configurar ManagerProductos:Endpoint, RutEmpresa y Token.");
        }

        var cuerpo = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ConsultaProd xmlns="{SoapNamespace}">
                  <rutEmpresa>{System.Security.SecurityElement.Escape(rutEmpresa)}</rutEmpresa>
                  <token>{System.Security.SecurityElement.Escape(token)}</token>
                </ConsultaProd>
              </soap:Body>
            </soap:Envelope>
            """;

        using var response = await EnviarConReintentos(endpoint, cuerpo, cancellationToken);
        var respuesta = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var documento = XDocument.Parse(respuesta);
        var productos = documento
            .Descendants()
            .Where(x => x.Name.LocalName == "consultaProd")
            .Select(x => new ProductoManager(
                x.Elements().FirstOrDefault(e => e.Name.LocalName == "codigo")?.Value.Trim() ?? string.Empty,
                x.Elements().FirstOrDefault(e => e.Name.LocalName == "nombre")?.Value.Trim() ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .ToList();

        return productos;
    }

    private async Task<HttpResponseMessage> EnviarConReintentos(
        string endpoint,
        string cuerpo,
        CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(cuerpo, Encoding.UTF8, "text/xml")
                };
                request.Headers.Add("SOAPAction", "\"http://manager.cl/productos/ConsultaProd\"");

                return await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null && intento < MaximoIntentosConexion)
            {
                _logger.LogWarning(
                    ex,
                    "No fue posible conectar con Manager al intentar actualizar productos. " +
                    "Se reintentará en {Pausa} (intento {Intento} de {MaximoIntentos}).",
                    PausaEntreIntentos,
                    intento,
                    MaximoIntentosConexion);

                await Task.Delay(PausaEntreIntentos, cancellationToken);
            }
        }
    }
}
