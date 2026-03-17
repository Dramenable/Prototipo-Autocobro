using CrudContactosMVC.Data;
using CrudContactosMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrudContactosMVC.Controllers
{
    public class ProductosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Productos
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var productos = await _context.Productos.ToListAsync();
            return View(productos);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Producto producto)
        {
            if (ModelState.IsValid)
            {
                _context.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(producto);
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, Producto producto)
        {
            if (id != producto.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(producto);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .FirstOrDefaultAsync(m => m.Id == id);

            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        [HttpGet]
        public async Task<IActionResult> Borrar(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .FirstOrDefaultAsync(m => m.Id == id);

            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Borrar(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // NEW: Autocobro view
        [HttpGet]
        public IActionResult Autocobro()
        {
            return View();
        }

        // NEW: JSON endpoint to get producto by id
        [HttpGet]
        public async Task<IActionResult> GetProductoJson(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return Json(new { success = false });
            }

            return Json(new { success = true, id = producto.Id, nombre = producto.Nombre, precio = producto.Precio });
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}
