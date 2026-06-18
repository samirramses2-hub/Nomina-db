using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PeriodosNominaController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PeriodosNominaController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<IEnumerable<PeriodoNomina>>> GetByEmpresa(int empresaId)
    {
        return await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.FechaInicio)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PeriodoNomina>> GetById(int id)
    {
        var periodo = await _context.PeriodosNomina.FindAsync(id);
        if (periodo is null) return NotFound();
        return periodo;
    }

    [HttpPost]
    public async Task<ActionResult<PeriodoNomina>> Create(CrearPeriodoNominaDto dto)
    {
        var empresaExiste = await _context.Empresas.AnyAsync(e => e.Id == dto.EmpresaId);
        if (!empresaExiste)
            return BadRequest("La empresa especificada no existe.");

        var periodoAbierto = await _context.PeriodosNomina
            .AnyAsync(p => p.EmpresaId == dto.EmpresaId && p.Estado == EstadoPeriodo.Abierto);
        if (periodoAbierto)
            return BadRequest("Ya existe un periodo abierto para esta empresa. Ciérralo antes de crear uno nuevo.");

        var periodo = new PeriodoNomina
        {
            EmpresaId      = dto.EmpresaId,
            FechaInicio    = dto.FechaInicio,
            FechaFin       = dto.FechaFin,
            TipoPeriodo    = (TipoPeriodo)dto.TipoPeriodo,
            EjercicioFiscal = dto.EjercicioFiscal,
            Estado         = EstadoPeriodo.Abierto,
            FechaCreacion  = DateTime.UtcNow
        };

        _context.PeriodosNomina.Add(periodo);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = periodo.Id }, periodo);
    }

    [HttpPatch("{id}/cerrar")]
    public async Task<IActionResult> Cerrar(int id)
    {
        var periodo = await _context.PeriodosNomina.FindAsync(id);
        if (periodo is null) return NotFound();
        if (periodo.Estado == EstadoPeriodo.Cerrado)
            return BadRequest("El periodo ya está cerrado.");

        periodo.Estado = EstadoPeriodo.Cerrado;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}