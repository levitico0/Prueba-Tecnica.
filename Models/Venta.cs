using System.ComponentModel.DataAnnotations.Schema;

namespace MockPruebaTecnica.Models
{
    public class Venta
    {
        [Column("id_venta")]
        public int Id { get; set; }
        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Column("fecha_venta")]
        public DateTime FechaVenta { get; set; }

        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [ForeignKey("IdCliente")]
        public Cliente Cliente { get; set; }

     

        [ForeignKey("IdProducto")]
        public Producto Producto { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("total_venta")]
        public decimal TotalVenta { get; set; } 
    }
}
