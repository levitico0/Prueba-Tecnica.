using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockPruebaTecnica.Data;
using MockPruebaTecnica.Models;
using System.Diagnostics;
using Newtonsoft.Json;

namespace MockPruebaTecnica.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(int? productoId, int? año, int? mes)
        {
            try
            {
                var query = _context.Ventas.AsQueryable();

                if (productoId.HasValue)
                {
                    query = query.Where(v => v.IdProducto == productoId.Value);
                }

                if (año.HasValue)
                {
                    query = query.Where(v => v.FechaVenta.Year == año.Value);
                }

                if (mes.HasValue)
                {
                    query = query.Where(v => v.FechaVenta.Month == mes.Value);
                }

                decimal totalVentas = await query.SumAsync(v => v.TotalVenta);
                int totalUnidades = await query.SumAsync(v => v.Cantidad);

                var totalVentasPorMes = await query
                    .GroupBy(venta => new { venta.FechaVenta.Year, venta.FechaVenta.Month })
                    .Select(grupo => new
                    {
                        Año = grupo.Key.Year,
                        Mes = grupo.Key.Month,
                        TotalVentas = grupo.Sum(venta => venta.TotalVenta)
                    })
                    .OrderBy(grupo => grupo.Año)
                    .ThenBy(grupo => grupo.Mes)
                    .ToListAsync();

                var top10ProductosMasVendidos = await query
                    .GroupBy(venta => venta.IdProducto)
                    .Select(grupo => new
                    {
                        ProductoId = grupo.Key,
                        Nombre = grupo.FirstOrDefault().Producto.NombreProducto,
                        TotalVendido = grupo.Sum(venta => venta.Cantidad)
                    })
                    .OrderByDescending(resultado => resultado.TotalVendido)
                    .Take(10)
                    .ToListAsync();

                var ventasPorCategoria = await query
                    .GroupBy(venta => venta.Producto.Categoria)
                    .Select(grupo => new
                    {
                        Categoria = grupo.Key,
                        TotalVentas = grupo.Sum(venta => venta.TotalVenta)
                    })
                    .OrderByDescending(resultado => resultado.TotalVentas)
                    .ToListAsync();

                // Preparamos los datos para la vista
                ViewBag.SeriesVentasPorMes = JsonConvert.SerializeObject(totalVentasPorMes.Select(grupo => grupo.TotalVentas).ToList());
                ViewBag.CategoriasVentasPorMes = JsonConvert.SerializeObject(totalVentasPorMes.Select(grupo => $"{grupo.Año}-{grupo.Mes}").ToList());

                ViewBag.TopVendidos = JsonConvert.SerializeObject(top10ProductosMasVendidos.Select(grupo => grupo.Nombre).ToList());
                ViewBag.CantidadTop = JsonConvert.SerializeObject(top10ProductosMasVendidos.Select(grupo => grupo.TotalVendido).ToList());
                ViewBag.VentasPorCategoria = JsonConvert.SerializeObject(ventasPorCategoria.Select(grupo => grupo.TotalVentas).ToList());
                ViewBag.Categorias = JsonConvert.SerializeObject(ventasPorCategoria.Select(grupo => grupo.Categoria).ToList());
                ViewBag.TotalVentas = totalVentas;
                ViewBag.TotalUnidades = totalUnidades;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Se produjo un error en la acción Index: {ex.Message}", ex);
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        public IActionResult CargaArchivo()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ocurrió un problema al cargar la vista de CargaArchivo: {ex.Message}", ex);
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CargarArchivo(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar un archivo de Excel válido.");
                return View("CargaArchivo");
            }

            if (!file.FileName.EndsWith(".xlsx"))
            {
                ModelState.AddModelError("", "El archivo debe estar en formato .xlsx.");
                return View("CargaArchivo");
            }

            try
            {
                using (var stream = file.OpenReadStream())
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataTable = reader.AsDataSet().Tables[0];

                    for (int fila = 1; fila < dataTable.Rows.Count; fila++)
                    {
                        try
                        {
                            string fecha_venta = dataTable.Rows[fila][0]?.ToString();
                            string nombre = dataTable.Rows[fila][1]?.ToString();
                            string apellido = dataTable.Rows[fila][2]?.ToString();
                            string correo_electronico = dataTable.Rows[fila][3]?.ToString();
                            string codigo_barras = dataTable.Rows[fila][4]?.ToString();
                            string nombre_producto = dataTable.Rows[fila][5]?.ToString();
                            string descripcion = dataTable.Rows[fila][6]?.ToString();
                            string categoria = dataTable.Rows[fila][7]?.ToString();
                            int cantidad = int.Parse(dataTable.Rows[fila][8]?.ToString() ?? "0");
                            decimal precio = decimal.Parse(dataTable.Rows[fila][9]?.ToString() ?? "0");
                            decimal total_venta = decimal.Parse(dataTable.Rows[fila][10]?.ToString() ?? "0");

                            Producto? producto = await _context.Productos.FirstOrDefaultAsync(p => p.CodigoBarras == codigo_barras);
                            Cliente? cliente = await _context.Clientes.FirstOrDefaultAsync(m => m.Nombre == nombre);

                            if (cliente == null)
                            {
                                cliente = new Cliente
                                {
                                    Nombre = nombre,
                                    Apellido = apellido,
                                    CorreoElectronico = correo_electronico
                                };
                                _context.Clientes.Add(cliente);
                                await _context.SaveChangesAsync();
                            }

                            if (producto == null)
                            {
                                producto = new Producto
                                {
                                    NombreProducto = nombre_producto,
                                    CodigoBarras = codigo_barras,
                                    Descripcion = descripcion,
                                    Categoria = categoria,
                                    Precio = precio
                                };
                                _context.Productos.Add(producto);
                                await _context.SaveChangesAsync();
                            }

                            if (!string.IsNullOrEmpty(fecha_venta))
                            {
                                Venta venta = new Venta
                                {
                                    FechaVenta = DateTime.Parse(fecha_venta),
                                    Cantidad = cantidad,
                                    TotalVenta = total_venta,
                                    IdCliente = cliente.Id,
                                    IdProducto = producto.Id
                                };
                                _context.Ventas.Add(venta);
                            }
                        }
                        catch (Exception rowEx)
                        {
                            _logger.LogError($"Hubo un error al procesar la fila {fila}: {rowEx.Message}", rowEx);
                            continue;
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error durante la carga del archivo: {ex.Message}", ex);
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
