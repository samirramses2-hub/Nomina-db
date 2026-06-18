using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.API.Reportes.Constancias;
using NominaApp.Infrastructure.Data;
using QuestPDF.Fluent;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConstanciasController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ConstanciasController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("carta-trabajo/{empleadoId}")]
    public async Task<IActionResult> GenerarCartaTrabajo(int empleadoId)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);

        if (empleado == null || empleado.Empresa == null)
            return NotFound("Empleado o empresa no encontrados.");

        var document = new CartaTrabajoDocument(empleado, empleado.Empresa);
        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Carta_Trabajo_{empleado.RFC}.pdf");
    }

    [HttpGet("percepciones/{empleadoId}/{ejercicio}")]
    public async Task<IActionResult> GenerarConstanciaPercepciones(int empleadoId, int ejercicio)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);

        if (empleado == null || empleado.Empresa == null)
            return NotFound("Empleado o empresa no encontrados.");

        // Para simplificar la demo, simulamos la suma de percepciones y retenciones del ejercicio
        // En producción real, esto se obtendría sumando los CFDI timbrados o el historial de nómina
        decimal totalPercepciones = 150000;
        decimal totalRetenciones = 20000;

        var document = new ConstanciaPercepcionesDocument(empleado, empleado.Empresa, ejercicio, totalPercepciones, totalRetenciones);
        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Constancia_Percepciones_{ejercicio}_{empleado.RFC}.pdf");
    }
}