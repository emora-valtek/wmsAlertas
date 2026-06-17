namespace WMS.Alertas.Models
{

    public class PendienteIngresoDto
    {
        public string NumeroIngreso { get; set; }
        public string Documento { get; set; } = string.Empty;
        public string CodigoProducto { get; set; }
        public string Lote { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CantidadExistencias { get; set; }
        public string Estado { get; set; }
        public string UsuarioCreacion { get; set; }
        public int DiasSinIngresar { get; set; }
    }
}
