using CrudContactosMVC.Data;
using CrudContactosMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CrudContactosMVC.Controllers
{
    public class ProductosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string AutocobroCartSessionKey = "AutocobroCart";
        private const string AutocobroFirstDetectionProductsSessionKey = "AutocobroFirstDetectionProducts";

        public ProductosController(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
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
            var model = new AutocobroViewModel
            {
                CartItems = GetCartItems(),
                Mensaje = TempData["AutocobroMensaje"] as string,
                PagoRealizado = TempData["PagoRealizado"] as bool? == true
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutocobroRegistrar(AutocobroViewModel model, IFormFile? jsonFile)
        {
            if (jsonFile != null && jsonFile.Length > 0)
            {
                var (registradosDesdeJson, noEncontradosDesdeJson) = await RegistrarDesdeJsonFileAsync(jsonFile);

                if (registradosDesdeJson == 0)
                {
                    TempData["AutocobroMensaje"] = "No se registraron productos desde el archivo JSON";
                }
                else if (noEncontradosDesdeJson.Any())
                {
                    TempData["AutocobroMensaje"] = $"Registrados: {registradosDesdeJson}. No encontrados: {string.Join(", ", noEncontradosDesdeJson)}";
                }
                else
                {
                    TempData["AutocobroMensaje"] = $"Se registraron {registradosDesdeJson} productos desde JSON";
                }

                return RedirectToAction(nameof(Autocobro));
            }

            if (string.IsNullOrWhiteSpace(model.ProductName))
            {
                TempData["AutocobroMensaje"] = "Ingrese un nombre de producto válido";
                return RedirectToAction(nameof(Autocobro));
            }

            var productName = model.ProductName.Trim();
            var cartItems = GetCartItems();
            var registrado = await RegistrarProductoPorNombreAsync(productName, cartItems);

            if (!registrado)
            {
                TempData["AutocobroMensaje"] = "Producto no encontrado";
                return RedirectToAction(nameof(Autocobro));
            }

            SaveCartItems(cartItems);
            TempData["AutocobroMensaje"] = $"Producto '{productName}' registrado";
            return RedirectToAction(nameof(Autocobro));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AutocobroCancelar()
        {
            HttpContext.Session.Remove(AutocobroCartSessionKey);
            HttpContext.Session.Remove(AutocobroFirstDetectionProductsSessionKey);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AutocobroPagar()
        {
            var cartItems = GetCartItems();
            if (!cartItems.Any())
            {
                TempData["AutocobroMensaje"] = "El carrito está vacío";
                return RedirectToAction(nameof(Autocobro));
            }

            // Actualizar inventario: restar la cantidad comprada a cada producto
            foreach (var item in cartItems)
            {
                var producto = _context.Productos.FirstOrDefault(p => p.Id == item.Id);
                if (producto != null)
                {
                    producto.Cantidad = Math.Max(0, producto.Cantidad - item.Cantidad);
                }
            }
            _context.SaveChanges();

            HttpContext.Session.Remove(AutocobroCartSessionKey);
            HttpContext.Session.Remove(AutocobroFirstDetectionProductsSessionKey);
            TempData["PagoRealizado"] = true;
            TempData["AutocobroMensaje"] = "Pago realizado";

            return RedirectToAction(nameof(Autocobro));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DetectarProductos(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No se recibió una imagen." });
            }

            try
            {
                var responseContent = await EnviarImagenADeteccionAsync(file);

                var detectedProducts = NormalizarProductos(ExtraerNombresDesdeJson(responseContent));
                HttpContext.Session.SetString(AutocobroFirstDetectionProductsSessionKey, JsonSerializer.Serialize(detectedProducts));

                using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(responseContent));
                IFormFile jsonFile = new FormFile(jsonStream, 0, jsonStream.Length, "jsonFile", "detect-response.json")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/json"
                };

                await AutocobroRegistrar(new AutocobroViewModel(), jsonFile);
                var cartItems = GetCartItems();
                var mensaje = TempData["AutocobroMensaje"] as string;

                return Json(new
                {
                    success = true,
                    responseJson = FormatJson(responseContent),
                    mensaje,
                    cartItems = cartItems.Select(item => new
                    {
                        id = item.Id,
                        nombre = item.Nombre,
                        precio = item.Precio,
                        cantidad = item.Cantidad,
                        subtotal = item.Subtotal
                    }),
                    total = cartItems.Sum(item => item.Subtotal)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al llamar al API de detección.", detalle = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>  ValidarPagoConSegundaDeteccion(IFormFile file)
        {
            // Valida que llegue una imagen desde el navegador.
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, mensaje = "No se recibió una imagen." });
            }

            // Recupera la detección inicial guardada en sesión.
            var firstDetectionJson = HttpContext.Session.GetString(AutocobroFirstDetectionProductsSessionKey);
            if (string.IsNullOrWhiteSpace(firstDetectionJson))
            {
                return BadRequest(new { success = false, mensaje = "Primero debe realizar la detección inicial." });
            }

            // Convierte la detección inicial a lista de productos.
            var firstProducts = JsonSerializer.Deserialize<List<string>>(firstDetectionJson) ?? new List<string>();

            try
            {
                // Envía la nueva imagen al API y obtiene el JSON de respuesta.
                var responseContent = await EnviarImagenADeteccionAsync(file);
                // Extrae y normaliza los productos detectados en la segunda imagen.
                var secondProducts = NormalizarProductos(ExtraerNombresDesdeJson(responseContent));

                // Compara ambas listas (mismos elementos y orden, ignorando mayúsculas/minúsculas).
                var coinciden = firstProducts.SequenceEqual(secondProducts, StringComparer.OrdinalIgnoreCase);

                // Devuelve el resultado de la comparación junto con ambas listas.
                return Json(new
                {
                    success = coinciden,
                    mensaje = coinciden
                        ? "Los productos coinciden."
                        : "Los productos detectados no coinciden con los productos pagados. Por favor espere asistencia.",
                    responseJson = FormatJson(responseContent),
                    firstProducts,
                    secondProducts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, mensaje = "Error al validar la segunda detección.", detalle = ex.Message });
            }
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }

        private List<AutocobroItemViewModel> GetCartItems()
        {
            var cartJson = HttpContext.Session.GetString(AutocobroCartSessionKey);
            if (string.IsNullOrWhiteSpace(cartJson))
            {
                return new List<AutocobroItemViewModel>();
            }

            var cartItems = JsonSerializer.Deserialize<List<AutocobroItemViewModel>>(cartJson);
            return cartItems ?? new List<AutocobroItemViewModel>();
        }

        private void SaveCartItems(List<AutocobroItemViewModel> cartItems)
        {
            var cartJson = JsonSerializer.Serialize(cartItems);
            HttpContext.Session.SetString(AutocobroCartSessionKey, cartJson);
        }

        private static string FormatJson(string json)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }

        private async Task<string> EnviarImagenADeteccionAsync(IFormFile file)
        {
            // Prepara un body multipart/form-data para poder enviar el archivo al API externo.
            // El endpoint /detect exige un campo tipo archivo llamado "file".
            using var form = new MultipartFormDataContent();

            // Abre el stream del archivo recibido desde el navegador (la captura de cámara).
            await using var fileStream = file.OpenReadStream();

            // Envuelve el stream en contenido HTTP para adjuntarlo al multipart.
            using var streamContent = new StreamContent(fileStream);

            // Define el Content-Type del archivo. Si el navegador no lo manda, usa octet-stream.
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

            // Agrega el archivo al form con el nombre de campo "file" (requerido por el API).
            // También se envía un nombre de archivo para que el API pueda identificarlo.
            form.Add(streamContent, "file", string.IsNullOrWhiteSpace(file.FileName) ? "capture.jpg" : file.FileName);

            // Obtiene un HttpClient desde la factory (recomendado para manejar el ciclo de vida del cliente).
            var client = _httpClientFactory.CreateClient();

            // Envía el POST al API de detección (FastAPI o similar) que corre en localhost:8000.
            using var response = await client.PostAsync("http://localhost:8000/detect", form);

            // Lee el contenido de la respuesta como texto (normalmente JSON).
            var responseContent = await response.Content.ReadAsStringAsync();

            // Si el API devolvió error, se lanza una excepción con el contenido de la respuesta
            // para que el controlador pueda responder con StatusCode(500) al cliente.
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(responseContent);
            }

            // Devuelve el JSON (texto) generado por el API de detección.
            return responseContent;
        }

        private async Task<(int Registrados, List<string> NoEncontrados)> RegistrarDesdeJsonFileAsync(IFormFile jsonFile)
        {
            using var reader = new StreamReader(jsonFile.OpenReadStream());
            var jsonContent = await reader.ReadToEndAsync();
            var productNames = ExtraerNombresDesdeJson(jsonContent);

            var cartItems = GetCartItems();
            var noEncontrados = new List<string>();
            var registrados = 0;

            foreach (var productName in productNames)
            {
                var registrado = await RegistrarProductoPorNombreAsync(productName, cartItems);
                if (registrado)
                {
                    registrados++;
                }
                else
                {
                    noEncontrados.Add(productName);
                }
            }

            SaveCartItems(cartItems);
            return (registrados, noEncontrados);
        }

        private async Task<bool> RegistrarProductoPorNombreAsync(string productName, List<AutocobroItemViewModel> cartItems)
        {
            var normalizedName = productName.Trim().ToLower();
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Nombre.ToLower() == normalizedName);
            if (producto == null)
            {
                return false;
            }

            var existing = cartItems.FirstOrDefault(x => x.Id == producto.Id);
            if (existing == null)
            {
                cartItems.Add(new AutocobroItemViewModel
                {
                    Id = producto.Id,
                    Nombre = producto.Nombre,
                    Precio = producto.Precio,
                    Cantidad = 1
                });
            }
            else
            {
                existing.Cantidad += 1;
            }

            return true;
        }

        private static List<string> ExtraerNombresDesdeJson(string jsonContent)
        {
            var nombres = new List<string>();

            try
            {
                using var jsonDoc = JsonDocument.Parse(jsonContent);
                ExtraerTextosJson(jsonDoc.RootElement, nombres);
            }
            catch
            {
                return new List<string>();
            }

            return nombres
                .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizarProductos(IEnumerable<string> productos)
        {
            return productos
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private static void ExtraerTextosJson(JsonElement element, List<string> nombres)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        nombres.Add(value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        ExtraerTextosJson(item, nombres);
                    }
                    break;
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        ExtraerTextosJson(prop.Value, nombres);
                    }
                    break;
            }
        }
    }
}
