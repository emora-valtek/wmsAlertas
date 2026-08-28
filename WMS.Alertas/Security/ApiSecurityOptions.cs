namespace WMS.Alertas.Security;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";
    public const string HeaderName = "X-API-Key";

    public string ApiKey { get; set; } = string.Empty;
}
