namespace WMS.Alertas.Models
{
    public class PendienteIngresoDetalleDto
    {
        public string NumeroIngreso { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public long Secuencia { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string UsuarioCreacion { get; set; } = string.Empty;
        public int DiasSinIngresar { get; set; }
    }
}
