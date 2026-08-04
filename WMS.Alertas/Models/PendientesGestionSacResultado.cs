namespace WMS.Alertas.Models;

public class PendientesGestionSacResultado
{
    public List<StockAsignadoSinPL> StockSinPackingList { get; set; } = new();

    public List<PackingListDevueltoSac> PackingListsDevueltos { get; set; } = new();

    public int CantidadTotal => StockSinPackingList.Count + PackingListsDevueltos.Count;
}
