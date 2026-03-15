using CrudContactosMVC.Data;
using CrudContactosMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrudContactosMVC.Controllers
{
    public class ContactosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Contactos
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var contactos = await _context.Contactos.ToListAsync();
            return View(contactos);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Contacto contacto)
        {
            if (ModelState.IsValid)
            {
                //Es donde guardamos en la BD
                _context.Add(contacto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(contacto);
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contacto = await _context.Contactos.FindAsync(id);

            if (contacto == null)
            {
                return NotFound();
            }


            return View(contacto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, Contacto contacto)
        {
            if (id != contacto.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(contacto);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactoExists(contacto.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(contacto);
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contacto = await _context.Contactos
                .FirstOrDefaultAsync(m => m.Id == id);

            if (contacto == null)
            {
                return NotFound();
            }

            return View(contacto);
        }

        [HttpGet]
        public async Task<IActionResult> Borrar(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contacto = await _context.Contactos
                .FirstOrDefaultAsync(m => m.Id == id);

            if (contacto == null)
            {
                return NotFound();
            }

            return View(contacto);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Borrar(int id)
        {
           var contacto = await _context.Contactos.FindAsync(id);

            if (contacto != null)
            {
                _context.Contactos.Remove(contacto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }


        private bool ContactoExists(int id)
        {
            return _context.Contactos.Any(e => e.Id == id);
        }
    }
}
