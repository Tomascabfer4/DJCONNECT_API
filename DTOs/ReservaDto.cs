namespace API_DJCONNECT.DTOs
{
    public class ReservaDto
    {
        public int Id { get; set; }
        public string Fecha { get; set; } // Lo pasamos como texto para que se lea fácil
        public string NombreDj { get; set; } = string.Empty;
        public string NombreCliente { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
