using Microsoft.AspNetCore.Mvc;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BeneficiosController : ControllerBase
{
    // Simulación en memoria para el prototipo
    private static int _puntosActuales = 3250;
    private static readonly List<object> _historial = new()
    {
        new { id = 1, fecha = "01/06/2026", descripcion = "Asistencia Perfecta Mayo", puntos = 500, tipo = "ganado" },
        new { id = 2, fecha = "15/05/2026", descripcion = "Completar Onboarding", puntos = 1000, tipo = "ganado" },
        new { id = 3, fecha = "10/05/2026", descripcion = "Bono de Antigüedad (1 año)", puntos = 1500, tipo = "ganado" },
        new { id = 4, fecha = "05/05/2026", descripcion = "Registro Inicial", puntos = 250, tipo = "ganado" }
    };

    private static readonly List<object> _catalogo = new()
    {
        new { id = 101, titulo = "Viernes de Home Office", descripcion = "Trabaja desde casa este viernes.", puntosCosto = 1000, icono = "🏠", color = "#10b981" },
        new { id = 102, titulo = "Medio Día Libre", descripcion = "Tómate la mañana o tarde libre cualquier día de la semana.", puntosCosto = 2500, icono = "⏱️", color = "#3b82f6" },
        new { id = 103, titulo = "Bono Amazon $500", descripcion = "Tarjeta de regalo digital de Amazon México.", puntosCosto = 5000, icono = "🎁", color = "#f59e0b" },
        new { id = 104, titulo = "Día Libre Completo", descripcion = "Un día extra de vacaciones con goce de sueldo.", puntosCosto = 8000, icono = "🌴", color = "#8b5cf6" },
        new { id = 105, titulo = "Desayuno Sorpresa", descripcion = "Envío de desayuno a tu casa u oficina.", puntosCosto = 1500, icono = "☕", color = "#ec4899" }
    };

    [HttpGet("{empleadoId}")]
    public IActionResult GetBeneficios(int empleadoId)
    {
        string nivel = _puntosActuales >= 5000 ? "Oro" : _puntosActuales >= 2000 ? "Plata" : "Bronce";
        int puntosSiguienteNivel = _puntosActuales >= 5000 ? 10000 : _puntosActuales >= 2000 ? 5000 : 2000;
        int progreso = (int)((_puntosActuales / (double)puntosSiguienteNivel) * 100);

        return Ok(new
        {
            saldoPuntos = _puntosActuales,
            nivelActual = nivel,
            progresoNivel = progreso,
            puntosSiguienteNivel,
            historial = _historial,
            catalogo = _catalogo
        });
    }

    [HttpPost("canjear")]
    public IActionResult Canjear([FromBody] CanjeRequestDto request)
    {
        dynamic item = _catalogo.FirstOrDefault(x => (int)x.GetType().GetProperty("id").GetValue(x) == request.RecompensaId);
        
        if (item == null) return NotFound("Recompensa no encontrada.");
        
        int costo = (int)item.GetType().GetProperty("puntosCosto").GetValue(item);
        string titulo = (string)item.GetType().GetProperty("titulo").GetValue(item);

        if (_puntosActuales < costo)
        {
            return BadRequest($"No tienes suficientes puntos. Necesitas {costo} puntos.");
        }

        _puntosActuales -= costo;

        _historial.Insert(0, new
        {
            id = new Random().Next(1000, 9000),
            fecha = DateTime.UtcNow.ToString("dd/MM/yyyy"),
            descripcion = $"Canje: {titulo}",
            puntos = costo,
            tipo = "gastado"
        });

        return Ok(new { message = "Canje exitoso", nuevoSaldo = _puntosActuales });
    }
}

public class CanjeRequestDto
{
    public int EmpleadoId { get; set; }
    public int RecompensaId { get; set; }
}
