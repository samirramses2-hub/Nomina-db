using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.API.Reportes.Contratos;
using NominaApp.Infrastructure.Data;
using QuestPDF.Fluent;
using System;
using System.Threading.Tasks;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContratosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ContratosController(NominaDbContext context)
    {
        _context = context;
    }

    public class FirmaRequest
    {
        public string FirmaBase64 { get; set; } = string.Empty;
        public string TipoContrato { get; set; } = "POR TIEMPO INDETERMINADO";
    }

    [HttpPost("generar/{empleadoId}")]
    public async Task<IActionResult> GenerarContratoFirma(int empleadoId, [FromBody] FirmaRequest req)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);

        if (empleado == null || empleado.Empresa == null)
            return NotFound("Empleado o empresa no encontrados.");

        var document = new ContratoDocument(empleado, empleado.Empresa, req.FirmaBase64, req.TipoContrato);
        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Contrato_{empleado.RFC}.pdf");
    }
}
