using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmpleadosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public EmpleadosController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Empleado>>> GetAll()
    {
        return await _context.Empleados
            .Include(e => e.Empresa)
            .Where(e => e.Activo)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Empleado>> GetById(int id)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (empleado is null) return NotFound();
        return empleado;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<IEnumerable<Empleado>>> GetByEmpresa(int empresaId)
    {
        return await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Empleado>> Create(CrearEmpleadoDto dto)
    {
        var empresaExiste = await _context.Empresas.AnyAsync(e => e.Id == dto.EmpresaId);
        if (!empresaExiste)
            return BadRequest("La empresa especificada no existe.");

        var empleado = new Empleado
        {
            EmpresaId       = dto.EmpresaId,
            CodigoEmpleado = dto.CodigoEmpleado ?? $"EMP{DateTime.UtcNow:yyyyMMddHHmmss}",
            Nombre          = dto.Nombre,
            ApellidoPaterno = dto.ApellidoPaterno,
            ApellidoMaterno = dto.ApellidoMaterno,
            RFC             = dto.RFC,
            CURP            = dto.CURP,
            NSS             = dto.NSS,
            SalarioDiario   = dto.SalarioDiario,
            TipoContrato    = (TipoContrato)dto.TipoContrato,
            TipoPeriodo     = (TipoPeriodo)dto.TipoPeriodo,
            FechaIngreso    = dto.FechaIngreso,
            Activo          = true,
            FechaCreacion   = DateTime.UtcNow,
Banco          = dto.Banco,
CuentaBancaria = dto.CuentaBancaria,
CLABE          = dto.CLABE,
        };

        _context.Empleados.Add(empleado);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = empleado.Id }, empleado);
    }

    [HttpPatch("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var empleado = await _context.Empleados.FindAsync(id);
        if (empleado is null) return NotFound();

        empleado.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/perfil")]
public async Task<ActionResult<object>> GetPerfil(int id)
{
    var empleado = await _context.Empleados
        .Include(e => e.Empresa)
        .FirstOrDefaultAsync(e => e.Id == id);
    if (empleado is null) return NotFound();

    var incidencias = await _context.Incidencias
        .Include(i => i.PeriodoNomina)
        .Where(i => i.EmpleadoId == id)
        .OrderByDescending(i => i.FechaRegistro)
        .ToListAsync();

    var periodos = await _context.PeriodosNomina
        .Where(p => p.EmpresaId == empleado.EmpresaId)
        .OrderByDescending(p => p.FechaInicio)
        .ToListAsync();

    var historial = new List<object>();
    foreach (var periodo in periodos)
    {
        var incEmp = incidencias.Where(i => i.PeriodoNominaId == periodo.Id).ToList();
        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var parametros = new NominaApp.Core.Calculos.ParametrosCalculo
        {
            SalarioDiario        = empleado.SalarioDiario,
            DiasPeriodo          = diasPeriodo,
            EjercicioFiscal      = periodo.EjercicioFiscal,
            FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
            FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
            DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
            HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
            HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
            HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
            Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
            DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
        };

        var calculo = NominaApp.Core.Calculos.MotorCalculo.Calcular(parametros);
        historial.Add(new
        {
            periodoId         = periodo.Id,
            periodo           = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            estado            = periodo.Estado.ToString(),
            percepciones      = calculo.TotalPercepciones,
            deducciones       = calculo.TotalDeducciones,
            neto              = calculo.NetoPagar,
            isrRetenido       = calculo.DetalleISR.ISRRetenido,
            incidencias       = incEmp.Count
        });
    }

    var antiguedad = (DateTime.UtcNow - empleado.FechaIngreso).Days;

    return Ok(new
    {
        empleado = new
        {
            empleado.Id,
            empleado.CodigoEmpleado,
            nombreCompleto   = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
            empleado.RFC,
            empleado.CURP,
            empleado.NSS,
            empleado.SalarioDiario,
            tipoContrato     = empleado.TipoContrato.ToString(),
            tipoPeriodo      = empleado.TipoPeriodo.ToString(),
            fechaIngreso     = empleado.FechaIngreso.ToString("dd/MM/yyyy"),
            antiguedadDias   = antiguedad,
            antiguedadTexto  = $"{antiguedad / 365} año(s) {(antiguedad % 365) / 30} mes(es)",
            empleado.Banco,
            empleado.CLABE,
            empresa          = empleado.Empresa.RazonSocial
        },
        historial,
        totalPeriodos    = historial.Count,
        totalNeto        = historial.Sum(h => (decimal)h.GetType().GetProperty("neto")!.GetValue(h)!),
        totalISR         = historial.Sum(h => (decimal)h.GetType().GetProperty("isrRetenido")!.GetValue(h)!)
    });
    
}

[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, CrearEmpleadoDto dto)
{
    var empleado = await _context.Empleados.FindAsync(id);
    if (empleado is null) return NotFound();

    empleado.Nombre          = dto.Nombre;
    empleado.ApellidoPaterno = dto.ApellidoPaterno;
    empleado.ApellidoMaterno = dto.ApellidoMaterno;
    empleado.RFC             = dto.RFC;
    empleado.CURP            = dto.CURP;
    empleado.NSS             = dto.NSS;
    empleado.SalarioDiario   = dto.SalarioDiario;
    empleado.TipoContrato    = (TipoContrato)dto.TipoContrato;
    empleado.TipoPeriodo     = (TipoPeriodo)dto.TipoPeriodo;
    empleado.DepartamentoId = dto.DepartamentoId;
    empleado.Puesto         = dto.Puesto;
    empleado.FechaIngreso    = dto.FechaIngreso;
    empleado.CodigoEmpleado  = dto.CodigoEmpleado ?? empleado.CodigoEmpleado;
    empleado.Banco           = dto.Banco;
    empleado.CuentaBancaria  = dto.CuentaBancaria;
    empleado.CLABE           = dto.CLABE;

    await _context.SaveChangesAsync();
    return NoContent();
}
}