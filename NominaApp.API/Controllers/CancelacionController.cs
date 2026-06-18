using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.Infrastructure.Servicios;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CancelacionController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const string FacturamaUsuario  = "Samir0501";
    private const string FacturamaPassword = "Srpv180501";

    public CancelacionController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetCFDIs(int empresaId)
    {
        var cfdis = await _context.CFDIs
            .Include(c => c.Empleado)
            .Include(c => c.PeriodoNomina)
            .Where(c => c.Empleado.EmpresaId == empresaId)
            .OrderByDescending(c => c.FechaTimbrado)
            .ToListAsync();

        return Ok(cfdis.Select(c => new
        {
            c.Id,
            c.UUID,
            c.RFCEmisor,
            c.RFCReceptor,
            empleado      = $"{c.Empleado.Nombre} {c.Empleado.ApellidoPaterno}".Trim(),
            periodo       = $"{c.PeriodoNomina.FechaInicio:dd/MM/yyyy} — {c.PeriodoNomina.FechaFin:dd/MM/yyyy}",
            c.Total,
            estado        = c.Estado.ToString(),
            fechaTimbrado = c.FechaTimbrado.ToString("dd/MM/yyyy HH:mm"),
            fechaCancelacion = c.FechaCancelacion?.ToString("dd/MM/yyyy HH:mm"),
            c.MotivoCancelacion,
            c.UUIDSustitucion,
            cancelable    = c.Estado == EstadoCFDI.Vigente
        }));
    }

    [HttpPost("cancelar/{cfdiId}")]
    public async Task<ActionResult<object>> Cancelar(int cfdiId, [FromBody] CancelacionDto dto)
    {
        var cfdi = await _context.CFDIs
            .Include(c => c.Empleado)
            .ThenInclude(e => e.Empresa)
            .FirstOrDefaultAsync(c => c.Id == cfdiId);

        if (cfdi is null) return NotFound("CFDI no encontrado.");
        if (cfdi.Estado == EstadoCFDI.Cancelado)
            return BadRequest("Este CFDI ya fue cancelado.");

        // Validar que motivo 01 requiere UUID de sustitución
        if (dto.Motivo == "01" && string.IsNullOrEmpty(dto.UUIDSustitucion))
            return BadRequest("El motivo 01 (comprobante con errores) requiere el UUID del CFDI que lo sustituye.");

        // Llamar a Facturama para cancelar
        using var http = new HttpClient
        {
            BaseAddress = new Uri("https://apisandbox.facturama.mx")
        };
        var credenciales = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{FacturamaUsuario}:{FacturamaPassword}"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credenciales);

        var url = $"/api/3/cfdis/{cfdi.UUID}?motive={dto.Motivo}";
        if (!string.IsNullOrEmpty(dto.UUIDSustitucion))
            url += $"&uuidReplacement={dto.UUIDSustitucion}";

        var response = await http.DeleteAsync(url);
        var body     = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Cancelación Status: {(int)response.StatusCode}");
        Console.WriteLine($"Cancelación Response: {body}");

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            // En sandbox simulamos cancelación exitosa
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                cfdi.Estado           = EstadoCFDI.Cancelado;
                cfdi.MotivoCancelacion = dto.Motivo;
                cfdi.UUIDSustitucion  = dto.UUIDSustitucion;
                cfdi.FechaCancelacion = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje          = "CFDI cancelado (simulado en sandbox).",
                    uuid             = cfdi.UUID,
                    motivo           = dto.Motivo,
                    descripcionMotivo = DescripcionMotivo(dto.Motivo),
                    fechaCancelacion = cfdi.FechaCancelacion?.ToString("dd/MM/yyyy HH:mm")
                });
            }
            return BadRequest(new { error = body });
        }

        cfdi.Estado            = EstadoCFDI.Cancelado;
        cfdi.MotivoCancelacion = dto.Motivo;
        cfdi.UUIDSustitucion   = dto.UUIDSustitucion;
        cfdi.FechaCancelacion  = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje          = "CFDI cancelado correctamente.",
            uuid             = cfdi.UUID,
            motivo           = dto.Motivo,
            descripcionMotivo = DescripcionMotivo(dto.Motivo),
            fechaCancelacion = cfdi.FechaCancelacion?.ToString("dd/MM/yyyy HH:mm")
        });
    }

    private string DescripcionMotivo(string motivo) => motivo switch
    {
        "01" => "Comprobante emitido con errores con relación",
        "02" => "Comprobante emitido con errores sin relación",
        "03" => "No se llevó a cabo la operación",
        "04" => "Operación nominativa relacionada en una factura global",
        _    => "Motivo desconocido"
    };
}

public class CancelacionDto
{
    public string Motivo { get; set; } = string.Empty;
    public string? UUIDSustitucion { get; set; }
    public string? Justificacion { get; set; }
}