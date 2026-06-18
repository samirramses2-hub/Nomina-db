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
public class PredictorIAController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PredictorIAController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("riesgos/{empresaId}")]
    public async Task<IActionResult> GetRiesgosRotacion(int empresaId)
    {
        // En una implementación real, aquí se llamaría a un modelo de Machine Learning (ej. Python, ML.NET)
        // enviando datos como: días de vacaciones tomados, horas extra, años de antigüedad, cambios de salario, etc.
        // Para esta demo espectacular, generaremos un score de riesgo simulado de forma heurística.

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .Select(e => new
            {
                e.Id,
                NombreCompleto = $"{e.Nombre} {e.ApellidoPaterno} {e.ApellidoMaterno}",
                e.Puesto,
                e.FechaIngreso,
                e.SalarioDiario
            })
            .ToListAsync();

        var random = new Random(empresaId); // Para que los datos sean consistentes en la demo

        var predicciones = empleados.Select(e =>
        {
            var antiguedad = (DateTime.Now - e.FechaIngreso).TotalDays / 365.0;
            var riesgoBase = random.Next(10, 40);

            // Simulamos lógica: si tienen más de 3 años, el riesgo baja un poco; si ganan poco, sube.
            if (antiguedad > 3) riesgoBase -= 10;
            if (e.SalarioDiario < 300) riesgoBase += 20;

            // Variabilidad aleatoria del modelo "IA"
            var riesgoFinal = Math.Clamp(riesgoBase + random.Next(-15, 25), 5, 95);

            string motivo = riesgoFinal > 60 
                ? (e.SalarioDiario < 400 ? "Baja competitividad salarial en mercado." : "Falta de plan de carrera a largo plazo.")
                : "Condiciones estables.";

            if (riesgoFinal > 80) motivo = "Alto volumen de horas extra y posible síndrome de Burnout.";

            return new
            {
                EmpleadoId = e.Id,
                Nombre = e.NombreCompleto,
                Puesto = e.Puesto,
                AntiguedadAnios = Math.Round(antiguedad, 1),
                RiesgoPorcentaje = riesgoFinal,
                MotivoPrincipal = motivo,
                Nivel = riesgoFinal > 70 ? "Alto" : riesgoFinal > 40 ? "Medio" : "Bajo"
            };
        })
        .OrderByDescending(p => p.RiesgoPorcentaje)
        .ToList();

        var resumen = new
        {
            AltoRiesgo = predicciones.Count(p => p.Nivel == "Alto"),
            RiesgoPromedio = Math.Round(predicciones.Average(p => p.RiesgoPorcentaje), 1),
            TotalAnalizados = predicciones.Count,
            Predicciones = predicciones
        };

        return Ok(resumen);
    }
}
