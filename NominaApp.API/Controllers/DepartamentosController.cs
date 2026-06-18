using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartamentosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public DepartamentosController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(int empresaId)
    {
        var deptos = await _context.Departamentos
            .Include(d => d.Jefe)
            .Include(d => d.Empleados)
            .Where(d => d.EmpresaId == empresaId && d.Activo)
            .OrderBy(d => d.Nombre)
            .ToListAsync();

        return Ok(deptos.Select(d => new
        {
            d.Id,
            d.Nombre,
            d.Descripcion,
            d.CodigoDepartamento,
            d.DepartamentoPadreId,
            jefeId         = d.JefeId,
            jefe           = d.Jefe != null
                ? $"{d.Jefe.Nombre} {d.Jefe.ApellidoPaterno}".Trim()
                : null,
            totalEmpleados = d.Empleados.Count(e => e.Activo),
            d.Activo,
            fechaCreacion  = d.FechaCreacion.ToString("dd/MM/yyyy")
        }));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Crear([FromBody] CrearDepartamentoDto dto)
    {
        var depto = new Departamento
        {
            EmpresaId           = dto.EmpresaId,
            Nombre              = dto.Nombre,
            Descripcion         = dto.Descripcion,
            CodigoDepartamento  = dto.CodigoDepartamento,
            JefeId              = dto.JefeId,
            DepartamentoPadreId = dto.DepartamentoPadreId,
            Activo              = true,
            FechaCreacion       = DateTime.UtcNow
        };
        _context.Departamentos.Add(depto);
        await _context.SaveChangesAsync();
        return Ok(new { depto.Id, mensaje = "Departamento creado correctamente." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CrearDepartamentoDto dto)
    {
        var depto = await _context.Departamentos.FindAsync(id);
        if (depto is null) return NotFound();
        depto.Nombre              = dto.Nombre;
        depto.Descripcion         = dto.Descripcion;
        depto.CodigoDepartamento  = dto.CodigoDepartamento;
        depto.JefeId              = dto.JefeId;
        depto.DepartamentoPadreId = dto.DepartamentoPadreId;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var depto = await _context.Departamentos.FindAsync(id);
        if (depto is null) return NotFound();
        depto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/asignar-empleado")]
    public async Task<IActionResult> AsignarEmpleado(int id, [FromBody] AsignarEmpleadoDto dto)
    {
        var empleado = await _context.Empleados.FindAsync(dto.EmpleadoId);
        if (empleado is null) return NotFound();
        empleado.DepartamentoId = id;
        if (!string.IsNullOrEmpty(dto.Puesto))
            empleado.Puesto = dto.Puesto;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Empleado asignado correctamente." });
    }

    [HttpGet("costo/{empresaId}/{periodoId}")]
    public async Task<ActionResult<object>> CostoPorDepartamento(int empresaId, int periodoId)
    {
        var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
        if (periodo is null) return NotFound();

        var deptos = await _context.Departamentos
            .Where(d => d.EmpresaId == empresaId && d.Activo)
            .ToListAsync();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

        var resultados = new List<object>();
        decimal totalGeneral = 0;

        // Por departamento
        foreach (var depto in deptos)
        {
            var empDepto = empleados.Where(e => e.DepartamentoId == depto.Id).ToList();
            decimal costoDepto = 0;
            decimal netoDepto  = 0;
            decimal isrDepto   = 0;
            decimal imssDepto  = 0;

            foreach (var emp in empDepto)
            {
                var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
                var calculo = MotorCalculo.Calcular(new ParametrosCalculo
                {
                    SalarioDiario        = emp.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = periodo.EjercicioFiscal,
                    FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                    FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                    Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                });
                var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
                costoDepto += calculo.TotalPercepciones + imss.TotalPatronal;
                netoDepto  += calculo.NetoPagar;
                isrDepto   += calculo.DetalleISR.ISRRetenido;
                imssDepto  += imss.TotalPatronal;
            }

            totalGeneral += costoDepto;
            resultados.Add(new
            {
                departamentoId = depto.Id,
                nombre         = depto.Nombre,
                codigo         = depto.CodigoDepartamento,
                empleados      = empDepto.Count,
                costoTotal     = Math.Round(costoDepto, 2),
                netoTotal      = Math.Round(netoDepto, 2),
                isrTotal       = Math.Round(isrDepto, 2),
                imssPatronal   = Math.Round(imssDepto, 2),
                porcentaje     = 0m // se calcula abajo
            });
        }

        // Sin departamento
        var empSinDepto = empleados.Where(e => e.DepartamentoId == null).ToList();
        if (empSinDepto.Any())
        {
            decimal costoSin = 0, netoSin = 0, isrSin = 0, imssSin = 0;
            foreach (var emp in empSinDepto)
            {
                var calculo = MotorCalculo.Calcular(new ParametrosCalculo
                {
                    SalarioDiario = emp.SalarioDiario,
                    DiasPeriodo   = diasPeriodo,
                    EjercicioFiscal = periodo.EjercicioFiscal
                });
                var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
                costoSin += calculo.TotalPercepciones + imss.TotalPatronal;
                netoSin  += calculo.NetoPagar;
                isrSin   += calculo.DetalleISR.ISRRetenido;
                imssSin  += imss.TotalPatronal;
            }
            totalGeneral += costoSin;
            resultados.Add(new
            {
                departamentoId = 0,
                nombre         = "Sin departamento",
                codigo         = (string?)null,
                empleados      = empSinDepto.Count,
                costoTotal     = Math.Round(costoSin, 2),
                netoTotal      = Math.Round(netoSin, 2),
                isrTotal       = Math.Round(isrSin, 2),
                imssPatronal   = Math.Round(imssSin, 2),
                porcentaje     = 0m
            });
        }

        // Calcular porcentajes
        var resultadosConPct = resultados.Select(r => new
        {
            departamentoId = (int)r.GetType().GetProperty("departamentoId")!.GetValue(r)!,
            nombre         = (string)r.GetType().GetProperty("nombre")!.GetValue(r)!,
            codigo         = r.GetType().GetProperty("codigo")!.GetValue(r) as string,
            empleados      = (int)r.GetType().GetProperty("empleados")!.GetValue(r)!,
            costoTotal     = (decimal)r.GetType().GetProperty("costoTotal")!.GetValue(r)!,
            netoTotal      = (decimal)r.GetType().GetProperty("netoTotal")!.GetValue(r)!,
            isrTotal       = (decimal)r.GetType().GetProperty("isrTotal")!.GetValue(r)!,
            imssPatronal   = (decimal)r.GetType().GetProperty("imssPatronal")!.GetValue(r)!,
            porcentaje     = totalGeneral > 0
                ? Math.Round((decimal)r.GetType().GetProperty("costoTotal")!.GetValue(r)! / totalGeneral * 100, 1)
                : 0m
        }).OrderByDescending(r => r.costoTotal).ToList();

        return Ok(new
        {
            periodo        = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            totalGeneral   = Math.Round(totalGeneral, 2),
            totalEmpleados = empleados.Count,
            departamentos  = resultadosConPct
        });
    }

    [HttpGet("organigrama/{empresaId}")]
    public async Task<ActionResult<object>> GetOrganigrama(int empresaId)
    {
        var deptos = await _context.Departamentos
            .Include(d => d.Jefe)
            .Include(d => d.Empleados)
            .Where(d => d.EmpresaId == empresaId && d.Activo)
            .ToListAsync();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        object BuildNode(Departamento d)
        {
            var hijos = deptos.Where(sub => sub.DepartamentoPadreId == d.Id).ToList();
            var emps  = empleados.Where(e => e.DepartamentoId == d.Id).ToList();
            return new
            {
                id             = d.Id,
                nombre         = d.Nombre,
                codigo         = d.CodigoDepartamento,
                jefe           = d.Jefe != null ? $"{d.Jefe.Nombre} {d.Jefe.ApellidoPaterno}".Trim() : null,
                totalEmpleados = emps.Count,
                empleados      = emps.Select(e => new
                {
                    e.Id,
                    nombre = $"{e.Nombre} {e.ApellidoPaterno}".Trim(),
                    e.Puesto,
                    e.SalarioDiario
                }).ToList(),
                subdepartamentos = hijos.Select(h => BuildNode(h)).ToList()
            };
        }

        var raices = deptos.Where(d => d.DepartamentoPadreId == null).ToList();
        var sinDepto = empleados.Where(e => e.DepartamentoId == null).ToList();

        return Ok(new
        {
            organigrama    = raices.Select(r => BuildNode(r)).ToList(),
            sinDepartamento = sinDepto.Select(e => new
            {
                e.Id,
                nombre  = $"{e.Nombre} {e.ApellidoPaterno}".Trim(),
                e.Puesto,
                e.SalarioDiario
            }).ToList()
        });
    }
}

public class CrearDepartamentoDto
{
    public int     EmpresaId           { get; set; }
    public string  Nombre              { get; set; } = string.Empty;
    public string? Descripcion         { get; set; }
    public string? CodigoDepartamento  { get; set; }
    public int?    JefeId              { get; set; }
    public int?    DepartamentoPadreId { get; set; }
}

public class AsignarEmpleadoDto
{
    public int     EmpleadoId { get; set; }
    public string? Puesto     { get; set; }
}