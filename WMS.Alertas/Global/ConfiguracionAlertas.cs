namespace WMS.Alertas.Global;

public static class ConfiguracionAlertas
{
    public const string ZonaHorariaChileId = "Pacific SA Standard Time";
    public const string TipoDestinatarioErrores = "ErrorWMSAlertas";
    public const string ColaManualQa = "qa-manual";
    public const int MinutosAntiguedadRespaldoPackingList = 15;

    public static readonly TimeZoneInfo ZonaHorariaChile =
        TimeZoneInfo.FindSystemTimeZoneById(ZonaHorariaChileId);
}
