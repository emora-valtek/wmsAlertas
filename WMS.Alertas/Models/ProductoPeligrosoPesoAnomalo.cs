namespace WMS.Alertas.Models;

public sealed class ProductoPeligrosoPesoAnomalo
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal? Peso { get; set; }
    public bool? EsInflamable { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string TipoAnomalia { get; set; } = string.Empty;
}
