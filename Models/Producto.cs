using System.ComponentModel.DataAnnotations.Schema;

namespace MockPruebaTecnica.Models
{
    public class Producto
    {
        [Column("id_producto")]
        public int Id { get; set; }

        [Column("nombre_producto")]
        public string NombreProducto { get; set; }

        [Column("precio")]
        public decimal Precio { get; set; }

        [Column("codigo_barras")]
        public string CodigoBarras { get; set; }

      
        [Column("descripcion")]
        public string Descripcion { get; set; }

        [Column("categoria")]
        public string Categoria { get; set; }

    
        
        public ICollection<Venta> Ventas { get; set; }
    }
}
