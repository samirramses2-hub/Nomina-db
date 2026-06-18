using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalarioController : ControllerBase
{
    private readonly NominaDbContext _context;

    public SalarioController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("historial/{empleadoId}")]
    public async Task<ActionResult<object>> GetHistorial(int empleadoId)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado is null) return NotFound();

        var historial = await _context.HistorialSalarial
            .Where(h => h.EmpleadoId == empleadoId)
            .OrderByDescending(h => h.FechaVigencia)
            .ToListAsync();

        // Si no hay historial crear uno con el salario actual
        if (!historial.Any())
        {
            var inicial = new HistorialSalarial
            {
                EmpleadoId    = empleadoId,
                SalarioDiario = empleado.SalarioDiario,
                FechaVigencia = empleado.FechaIngreso,
                FechaFin      = null,
                Motivo        = "Salario inicial de contratación",
                Activo        = true,
                FechaRegistro = DateTime.UtcNow
            };
            _context.HistorialSalarial.Add(inicial);
            await _context.SaveChangesAsync();
            historial = new List<HistorialSalarial> { inicial };
        }

        var incrementos = historial.Count > 1
            ? historial.Zip(historial.Skip(1), (nuevo, anterior) => new
            {
                de     = anterior.SalarioDiario,
                a      = nuevo.SalarioDiario,
                pct    = Math.Round((nuevo.SalarioDiario - anterior.SalarioDiario) / anterior.SalarioDiario * 100, 2),
                fecha  = nuevo.FechaVigencia.ToString("dd/MM/yyyy")
            }).ToList<object>()
            : new List<object>();

        return Ok(new
        {
            empleadoId,
            nombre         = $"{empleado.Nombre} {empleado.ApellidoPaterno}".Trim(),
            empresa        = empleado.Empresa.RazonSocial,
            salarioActual  = empleado.SalarioDiario,
            totalCambios   = historial.Count - 1,
            historial      = historial.Select(h => new
            {
                h.Id,
                h.SalarioDiario,
                salarioDiarioMensual = Math.Round(h.SalarioDiario * 30, 2),
                salarioDiarioAnual   = Math.Round(h.SalarioDiario * 365, 2),
                fechaVigencia = h.FechaVigencia.ToString("dd/MM/yyyy"),
                fechaFin      = h.FechaFin?.ToString("dd/MM/yyyy"),
                h.Motivo,
                h.Activo,
                vigente       = h.Activo && h.FechaFin == null
            }).ToList(),
            incrementos
        });
    }

    [HttpPost("cambio")]
    public async Task<ActionResult<object>> RegistrarCambio([FromBody] CambioSalarialDto dto)
    {
        var empleado = await _context.Empleados.FindAsync(dto.EmpleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        if (dto.SalarioDiario < 248.93m)
            return BadRequest("El nuevo salario no puede ser menor al mínimo vigente ($248.93/día).");

        if (dto.FechaVigencia < empleado.FechaIngreso)
            return BadRequest("La fecha de vigencia no puede ser anterior a la fecha de ingreso.");

        // Cerrar el salario anterior
        var salarioActivo = await _context.HistorialSalarial
            .FirstOrDefaultAsync(h => h.EmpleadoId == dto.EmpleadoId && h.Activo && h.FechaFin == null);

        if (salarioActivo != null)
        {
            salarioActivo.FechaFin = dto.FechaVigencia.AddDays(-1);
            salarioActivo.Activo   = false;
        }

        // Crear nuevo registro
        var nuevo = new HistorialSalarial
        {
            EmpleadoId    = dto.EmpleadoId,
            SalarioDiario = dto.SalarioDiario,
            FechaVigencia = dto.FechaVigencia,
            FechaFin      = null,
            Motivo        = dto.Motivo ?? "Modificación de salario",
            Activo        = true,
            FechaRegistro = DateTime.UtcNow
        };

        _context.HistorialSalarial.Add(nuevo);

        // Actualizar salario actual del empleado
        var salarioAnterior    = empleado.SalarioDiario;
        empleado.SalarioDiario = dto.SalarioDiario;

        await _context.SaveChangesAsync();

        var incremento = Math.Round((dto.SalarioDiario - salarioAnterior) / salarioAnterior * 100, 2);

        return Ok(new
        {
            mensaje        = "Cambio salarial registrado correctamente.",
            salarioAnterior,
            salarioNuevo   = dto.SalarioDiario,
            incremento     = $"{(incremento >= 0 ? "+" : "")}{incremento}%",
            fechaVigencia  = dto.FechaVigencia.ToString("dd/MM/yyyy")
        });
    }

    [HttpGet("vigente/{empleadoId}")]
    public async Task<ActionResult<object>> GetSalarioVigente(int empleadoId, [FromQuery] DateTime fecha)
    {
        // Obtener el salario que estaba vigente en una fecha específica
        var salario = await _context.HistorialSalarial
            .Where(h => h.EmpleadoId == empleadoId && h.FechaVigencia <= fecha)
            .OrderByDescending(h => h.FechaVigencia)
            .FirstOrDefaultAsync();

        if (salario is null)
        {
            var emp = await _context.Empleados.FindAsync(empleadoId);
            return Ok(new { salarioDiario = emp?.SalarioDiario ?? 0, fuente = "actual" });
        }

        return Ok(new
        {
            salario.SalarioDiario,
            vigenciaDesde = salario.FechaVigencia.ToString("dd/MM/yyyy"),
            vigenciaHasta = salario.FechaFin?.ToString("dd/MM/yyyy") ?? "Vigente",
            fuente        = "historial"
        });
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(int empresaId)
    {
        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var resultado = new List<object>();
        foreach (var emp in empleados)
        {
            var cambios = await _context.HistorialSalarial
                .Where(h => h.EmpleadoId == emp.Id)
                .CountAsync();

            var ultimo = await _context.HistorialSalarial
                .Where(h => h.EmpleadoId == emp.Id)
                .OrderByDescending(h => h.FechaVigencia)
                .FirstOrDefaultAsync();

            resultado.Add(new
            {
                empleadoId    = emp.Id,
                nombre        = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim(),
                salarioActual = emp.SalarioDiario,
                totalCambios  = cambios,
                ultimoCambio  = ultimo?.FechaVigencia.ToString("dd/MM/yyyy") ?? "Sin registros",
                ultimoMotivo  = ultimo?.Motivo ?? ""
            });
        }

        return Ok(resultado);
    }
}

public class CambioSalarialDto
{
    public int EmpleadoId { get; set; }
    public decimal SalarioDiario { get; set; }
    public DateTime FechaVigencia { get; set; }
    public string? Motivo { get; set; }
}