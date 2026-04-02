namespace CrudContactosMVC.Models
{
    public class AutocobroViewModel
    {
        public string? ProductName { get; set; }
        public List<AutocobroItemViewModel> CartItems { get; set; } = new();
        public decimal Total => CartItems.Sum(item => item.Subtotal);
        public bool PagoRealizado { get; set; }
        public string? Mensaje { get; set; }
    }

    public class AutocobroItemViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Cantidad { get; set; }
        public decimal Subtotal => Precio * Cantidad;
    }
}
