using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidenciasController : ControllerBase
{
    private readonly NominaDbContext _context;

    public IncidenciasController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("periodo/{periodoId}")]
    public async Task<ActionResult<IEnumerable<Incidencia>>> GetByPeriodo(int periodoId)
    {
        return await _context.Incidencias
            .Include(i => i.Empleado)
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();
    }

    [HttpGet("empleado/{empleadoId}")]
    public async Task<ActionResult<IEnumerable<Incidencia>>> GetByEmpleado(int empleadoId)
    {
        return await _context.Incidencias
            .Include(i => i.PeriodoNomina)
            .Where(i => i.EmpleadoId == empleadoId)
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Incidencia>> Create(CrearIncidenciaDto dto)
    {
        var empleado = await _context.Empleados.FindAsync(dto.EmpleadoId);
        if (empleado is null)
            return BadRequest("El empleado especificado no existe.");

        var periodo = await _context.PeriodosNomina.FindAsync(dto.PeriodoNominaId);
        if (periodo is null)
            return BadRequest("El periodo especificado no existe.");

        if (periodo.Estado == EstadoPeriodo.Cerrado)
            return BadRequest("No se pueden agregar incidencias a un periodo cerrado.");

        if (empleado.EmpresaId != periodo.EmpresaId)
            return BadRequest("El empleado no pertenece a la empresa del periodo.");

        // Validación de cantidad según tipo
        var tipo = (TipoIncidencia)dto.Tipo;
        if (tipo == TipoIncidencia.Vacaciones || tipo == TipoIncidencia.FaltaJustificada || tipo == TipoIncidencia.FaltaInjustificada)
        {
            if (dto.Cantidad <= 0 || dto.Cantidad > 30)
                return BadRequest("La cantidad de días debe ser entre 1 y 30.");
        }

        if (tipo == TipoIncidencia.HoraExtraSimple || tipo == TipoIncidencia.HoraExtraDoble || tipo == TipoIncidencia.HoraExtraTriple)
        {
            if (dto.Cantidad <= 0 || dto.Cantidad > 24)
                return BadRequest("La cantidad de horas debe ser entre 1 y 24.");
        }

        var incidencia = new Incidencia
        {
            EmpleadoId      = dto.EmpleadoId,
            PeriodoNominaId = dto.PeriodoNominaId,
            Tipo            = tipo,
            Cantidad        = dto.Cantidad,
            Observaciones   = dto.Observaciones,
            FechaRegistro   = DateTime.UtcNow
        };

        _context.Incidencias.Add(incidencia);
        await _context.SaveChangesAsync();
        return Ok(incidencia);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var incidencia = await _context.Incidencias
            .Include(i => i.PeriodoNomina)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia is null) return NotFound();

        if (incidencia.PeriodoNomina.Estado == EstadoPeriodo.Cerrado)
            return BadRequest("No se pueden eliminar incidencias de un periodo cerrado.");

        _context.Incidencias.Remove(incidencia);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}