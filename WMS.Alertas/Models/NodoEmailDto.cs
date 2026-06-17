namespace WMS.Alertas.Models
{
    public class NodoEmailDto
    {
        public string Email { get; set; }
        public string EmailContrasena { get; set; }
        public string Host { get; set; }
        public int Puerto { get; set; }
        public bool UsaSSL { get; set; }
        public string Alias { get; set; }
    }
}
