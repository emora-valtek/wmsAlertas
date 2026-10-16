using System.Text;
using System.Xml.Linq;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

/// <summary>Cliente mínimo del servicio SOAP ConsultaProd de Manager.</summary>
public sealed class ManagerProductosClient
{
    private const string SoapNamespace = "http://manager.cl/productos/";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ManagerProductosClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
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

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", "\"http://manager.cl/productos/ConsultaProd\"");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
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
}
