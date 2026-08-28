namespace WMS.Alertas.Global;

public enum ModoEjecucionAlertas
{
    Deshabilitado,
    Manual,
    Automatico
}

public sealed class ConfiguracionEjecucionAlertas
{
    public ConfiguracionEjecucionAlertas(
        ModoEjecucionAlertas modo,
        bool esProduccion,
        string ambiente,
        string? correoPruebas,
        bool usarCorreoPruebas)
    {
        Modo = modo;
        EsProduccion = esProduccion;
        Ambiente = ambiente;
        CorreoPruebas = correoPruebas;
        UsarCorreoPruebas = usarCorreoPruebas;
    }

    public ModoEjecucionAlertas Modo { get; }
    public bool Habilitadas => Modo != ModoEjecucionAlertas.Deshabilitado;
    public bool ProgramacionAutomatica =>
        Modo == ModoEjecucionAlertas.Automatico;
    public bool EjecucionManual => Modo == ModoEjecucionAlertas.Manual;
    public bool EsProduccion { get; }
    public string Ambiente { get; }
    public string? CorreoPruebas { get; }
    public bool UsarCorreoPruebas { get; }
}
