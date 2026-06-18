using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.Infrastructure.Servicios;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificacionesController : ControllerBase
{
    private readonly NominaDbContext _context;
    private readonly EmailService    _email;

    public NotificacionesController(NominaDbContext context, EmailService email)
    {
        _context = context;
        _email   = email;
    }

    // Notificar a empleado cuando se timbra su CFDI
    [HttpPost("recibo-timbrado")]
    public async Task<ActionResult<object>> NotificarReciboTimbrado([FromBody] NotificarReciboDto dto)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == dto.EmpleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        // Buscar usuario del empleado para obtener su email
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.EmpleadoId == dto.EmpleadoId && u.Activo);

        if (usuario is null || string.IsNullOrEmpty(usuario.Email))
            return Ok(new { mensaje = "Empleado sin usuario/email registrado. Notificación omitida.", enviado = false });

        var html = PlantillasEmail.ReciboTimbrado(
            nombreEmpleado: $"{empleado.Nombre} {empleado.ApellidoPaterno}",
            empresa:        empleado.Empresa.RazonSocial,
            periodo:        dto.Periodo,
            uuid:           dto.UUID,
            neto:           dto.Neto,
            isr:            dto.ISR,
            imssObrero:     dto.IMSSObrero,
            fechaPago:      dto.FechaPago
        );

        var exito = await _email.EnviarAsync(
            usuario.Email,
            $"{empleado.Nombre} {empleado.ApellidoPaterno}",
            $"Tu recibo de nómina está listo — {dto.Periodo}",
            html
        );

        return Ok(new { mensaje = exito ? "Email enviado correctamente." : "Error al enviar email.", enviado = exito });
    }

    // Alertar a contadores sobre periodo próximo a cerrar
    [HttpPost("alerta-cierre")]
    public async Task<ActionResult<object>> AlertaCierre([FromBody] AlertaCierreDto dto)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == dto.PeriodoId);
        if (periodo is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        // Calcular costo estimado
        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        decimal costoTotal = 0;
        foreach (var emp in empleados)
        {
            var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
            var calculo = MotorCalculo.Calcular(new ParametrosCalculo
            {
                SalarioDiario   = emp.SalarioDiario,
                DiasPeriodo     = diasPeriodo,
                EjercicioFiscal = periodo.EjercicioFiscal
            });
            costoTotal += calculo.TotalPercepciones + imss.TotalPatronal;
        }

        // Obtener contadores y administradores con acceso a esta empresa
        var contadores = await _context.Usuarios
            .Where(u => u.Activo
                     && (u.Rol == RolUsuario.Contador || u.Rol == RolUsuario.Administrador)
                     && (u.EmpresaId == null || u.EmpresaId == periodo.EmpresaId))
            .ToListAsync();

        if (!contadores.Any())
            return Ok(new { mensaje = "Sin contadores registrados.", enviados = 0 });

        var diasRestantes = (periodo.FechaFin.Date - DateTime.UtcNow.Date).Days;
        var enviados = 0;

        foreach (var contador in contadores.Where(c => !string.IsNullOrEmpty(c.Email)))
        {
            var html = PlantillasEmail.AlertaCierrePeriodo(
                nombreContador: contador.Nombre,
                empresa:        periodo.Empresa.RazonSocial,
                periodo:        $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
                fechaCierre:    periodo.FechaFin.ToString("dd/MM/yyyy"),
                diasRestantes:  diasRestantes,
                totalEmpleados: empleados.Count,
                costoTotal:     Math.Round(costoTotal, 2)
            );

            var exito = await _email.EnviarAsync(
                contador.Email, contador.Nombre,
                $"⚠️ El periodo cierra en {diasRestantes} día(s) — {periodo.Empresa.RazonSocial}",
                html
            );
            if (exito) enviados++;
        }

        return Ok(new { enviados, totalContadores = contadores.Count });
    }

    // Notificar error de timbrado
    [HttpPost("error-timbrado")]
    public async Task<ActionResult<object>> NotificarError([FromBody] ErrorTimbradoDto dto)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == dto.EmpleadoId);
        if (empleado is null) return NotFound();

        var periodo = await _context.PeriodosNomina.FindAsync(dto.PeriodoId);
        if (periodo is null) return NotFound();

        var contadores = await _context.Usuarios
            .Where(u => u.Activo
                     && (u.Rol == RolUsuario.Contador || u.Rol == RolUsuario.Administrador)
                     && (u.EmpresaId == null || u.EmpresaId == empleado.EmpresaId))
            .ToListAsync();

        var enviados = 0;
        foreach (var contador in contadores.Where(c => !string.IsNullOrEmpty(c.Email)))
        {
            var html = PlantillasEmail.ErrorTimbrado(
                nombreContador: contador.Nombre,
                empresa:        empleado.Empresa.RazonSocial,
                empleado:       $"{empleado.Nombre} {empleado.ApellidoPaterno}",
                periodo:        $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
                error:          dto.Error
            );

            var exito = await _email.EnviarAsync(
                contador.Email, contador.Nombre,
                $"🚨 Error al timbrar CFDI — {empleado.Nombre} {empleado.ApellidoPaterno}",
                html
            );
            if (exito) enviados++;
        }

        return Ok(new { enviados });
    }

    // Resumen de nómina procesada
    [HttpPost("resumen-nomina")]
    public async Task<ActionResult<object>> ResumenNomina([FromBody] ResumenNominaDto dto)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == dto.PeriodoId);
        if (periodo is null) return NotFound();

        var contadores = await _context.Usuarios
            .Where(u => u.Activo
                     && (u.Rol == RolUsuario.Contador || u.Rol == RolUsuario.Administrador)
                     && (u.EmpresaId == null || u.EmpresaId == periodo.EmpresaId))
            .ToListAsync();

        var enviados = 0;
        foreach (var contador in contadores.Where(c => !string.IsNullOrEmpty(c.Email)))
        {
            var html = PlantillasEmail.ResumenNomina(
                nombreContador: contador.Nombre,
                empresa:        periodo.Empresa.RazonSocial,
                periodo:        $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
                totalEmpleados: dto.TotalEmpleados,
                timbrados:      dto.Timbrados,
                errores:        dto.Errores,
                totalNeto:      dto.TotalNeto,
                costoEmpresa:   dto.CostoEmpresa
            );

            var exito = await _email.EnviarAsync(
                contador.Email, contador.Nombre,
                $"Nómina procesada — {periodo.Empresa.RazonSocial} — {periodo.FechaInicio:dd/MM/yyyy}",
                html
            );
            if (exito) enviados++;
        }

        return Ok(new { enviados });
    }

    // Verificar y enviar alertas automáticas de periodos próximos a cerrar
    [HttpPost("verificar-alertas")]
    public async Task<ActionResult<object>> VerificarAlertas()
    {
        var hoy = DateTime.UtcNow.Date;

        // Periodos que cierran en exactamente 3 días
        var periodosProximos = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .Where(p => p.Estado == EstadoPeriodo.Abierto
                     && p.FechaFin.Date == hoy.AddDays(3))
            .ToListAsync();

        var alertasEnviadas = 0;
        foreach (var periodo in periodosProximos)
        {
            var result = await AlertaCierre(new AlertaCierreDto { PeriodoId = periodo.Id });
            if (result.Value is not null)
                alertasEnviadas++;
        }

        // Empleados con salario menor al mínimo
        var empresas = await _context.Empresas.ToListAsync();
        foreach (var empresa in empresas)
        {
            var empMinimo = await _context.Empleados
                .Where(e => e.EmpresaId == empresa.Id && e.Activo && e.SalarioDiario < 248.93m)
                .ToListAsync();

            if (!empMinimo.Any()) continue;

            var contadores = await _context.Usuarios
                .Where(u => u.Activo
                         && (u.Rol == RolUsuario.Contador || u.Rol == RolUsuario.Administrador)
                         && (u.EmpresaId == null || u.EmpresaId == empresa.Id))
                .ToListAsync();

            foreach (var contador in contadores.Where(c => !string.IsNullOrEmpty(c.Email)))
            {
                var html = PlantillasEmail.AlertaSalarioMinimo(
                    contador.Nombre, empresa.RazonSocial,
                    empMinimo.Select(e => ($"{e.Nombre} {e.ApellidoPaterno}", e.SalarioDiario)).ToList()
                );
                await _email.EnviarAsync(
                    contador.Email, contador.Nombre,
                    $"🚨 Empleados con salario menor al mínimo — {empresa.RazonSocial}",
                    html
                );
            }
        }

        return Ok(new { periodosProximos = periodosProximos.Count, alertasEnviadas });
    }
}

public class NotificarReciboDto
{
    public int     EmpleadoId { get; set; }
    public string  Periodo    { get; set; } = string.Empty;
    public string  UUID       { get; set; } = string.Empty;
    public decimal Neto       { get; set; }
    public decimal ISR        { get; set; }
    public decimal IMSSObrero { get; set; }
    public string  FechaPago  { get; set; } = string.Empty;
}

public class AlertaCierreDto
{
    public int PeriodoId { get; set; }
}

public class ErrorTimbradoDto
{
    public int    EmpleadoId { get; set; }
    public int    PeriodoId  { get; set; }
    public string Error      { get; set; } = string.Empty;
}

public class ResumenNominaDto
{
    public int     PeriodoId      { get; set; }
    public int     TotalEmpleados { get; set; }
    public int     Timbrados      { get; set; }
    public int     Errores        { get; set; }
    public decimal TotalNeto      { get; set; }
    public decimal CostoEmpresa   { get; set; }
}