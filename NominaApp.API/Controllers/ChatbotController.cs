using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatbotController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ChatbotController(NominaDbContext context)
    {
        _context = context;
    }

    public class ChatRequest
    {
        public int EmpleadoId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    [HttpPost("preguntar")]
    public async Task<IActionResult> Preguntar([FromBody] ChatRequest req)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == req.EmpleadoId);

        if (empleado == null) {
            // Si el empleadoId 1 no existe y es un admin probando, devolvemos un mensaje amigable en lugar de error 404
            return Ok(new { Respuesta = "Hola Administrador. Estoy listo para ayudar a los empleados cuando inicien sesión en su portal. (Modo prueba: no hay empleado vinculado a esta cuenta)." });
        }

        var msg = req.Mensaje.ToLower();
        string respuesta = "No estoy seguro de entender tu pregunta. Puedes preguntarme sobre tus vacaciones, recibos de nómina, o cómo reportar una falta.";

        // Respuestas simuladas basadas en NLP simple
        if (msg.Contains("vacacion") || msg.Contains("días libres"))
        {
            respuesta = $"¡Hola {empleado.Nombre}! Con tu antigüedad y el nuevo esquema, tienes derecho a disfrutar tus días de vacaciones correspondientes. Si deseas solicitar un descanso, por favor dirígete al apartado de 'Mis Solicitudes' en el portal.";
        }
        else if (msg.Contains("recibo") || msg.Contains("pago") || msg.Contains("nomina") || msg.Contains("nómina"))
        {
            var cuenta = string.IsNullOrEmpty(empleado.CuentaBancaria) ? "XXXX" : empleado.CuentaBancaria.Substring(Math.Max(0, empleado.CuentaBancaria.Length - 4));
            respuesta = $"Tus recibos de nómina (CFDI) se timbran cada periodo (tienes periodo {empleado.TipoPeriodo}). Puedes descargarlos directamente desde la pantalla principal de tu portal de empleado. El último pago se depositó en tu cuenta terminación {cuenta}.";
        }
        else if (msg.Contains("falta") || msg.Contains("enferm") || msg.Contains("incapacidad"))
        {
            respuesta = "Si te encuentras enfermo o tuviste una emergencia, por favor notifícalo a Recursos Humanos. Para justificar una falta por incapacidad, recuerda traer el documento oficial del IMSS a la brevedad.";
        }
        else if (msg.Contains("hola") || msg.Contains("buenos dias") || msg.Contains("buenas tardes"))
        {
            respuesta = $"¡Hola {empleado.Nombre}! Soy el asistente virtual de RH. ¿En qué te puedo ayudar hoy?";
        }

        // Simulamos un pequeño retraso para dar sensación de IA procesando
        await Task.Delay(1500);

        return Ok(new { Respuesta = respuesta });
    }
}
