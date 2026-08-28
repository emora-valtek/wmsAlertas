using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WMS.Alertas.Security;

public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IOptionsMonitor<ApiSecurityOptions> _options;
    private readonly ILogger<ApiKeyEndpointFilter> _logger;

    public ApiKeyEndpointFilter(
        IOptionsMonitor<ApiSecurityOptions> options,
        ILogger<ApiKeyEndpointFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var apiKeyEsperada = _options.CurrentValue.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKeyEsperada))
        {
            _logger.LogCritical(
                "No se configuró {Section}:{Property}; el endpoint protegido no está disponible.",
                ApiSecurityOptions.SectionName,
                nameof(ApiSecurityOptions.ApiKey));

            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Servicio no configurado");
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(
                ApiSecurityOptions.HeaderName,
                out var valores) ||
            valores.Count != 1 ||
            !Coincide(valores[0], apiKeyEsperada))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }

    private static bool Coincide(string? recibida, string esperada)
    {
        if (string.IsNullOrEmpty(recibida))
            return false;

        var bytesRecibidos = Encoding.UTF8.GetBytes(recibida);
        var bytesEsperados = Encoding.UTF8.GetBytes(esperada);

        return bytesRecibidos.Length == bytesEsperados.Length &&
               CryptographicOperations.FixedTimeEquals(
                   bytesRecibidos,
                   bytesEsperados);
    }
}
