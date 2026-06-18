namespace NominaApp.Infrastructure.Servicios;

public static class PlantillasEmail
{
    private static string Base(string contenido) => $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 0; padding: 0; background: #f1f5f9; }}
    .container {{ max-width: 580px; margin: 32px auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 24px rgba(0,0,0,0.08); }}
    .header {{ background: #0f172a; padding: 24px 32px; }}
    .header-title {{ color: #38bdf8; font-size: 20px; font-weight: 700; margin: 0; }}
    .header-sub {{ color: #64748b; font-size: 12px; margin: 4px 0 0; }}
    .body {{ padding: 32px; }}
    .body h2 {{ color: #1e293b; font-size: 18px; font-weight: 600; margin: 0 0 16px; }}
    .body p {{ color: #374151; font-size: 14px; line-height: 1.6; margin: 0 0 12px; }}
    .card {{ background: #f8fafc; border-radius: 8px; padding: 16px 20px; margin: 16px 0; border-left: 4px solid #0ea5e9; }}
    .card-red {{ border-left-color: #ef4444; }}
    .card-green {{ border-left-color: #22c55e; }}
    .card-amber {{ border-left-color: #f59e0b; }}
    .label {{ font-size: 11px; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 4px; }}
    .value {{ font-size: 16px; font-weight: 600; color: #1e293b; }}
    .value-big {{ font-size: 28px; font-weight: 700; color: #0ea5e9; }}
    .btn {{ display: inline-block; background: #0ea5e9; color: #fff; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-size: 14px; font-weight: 500; margin: 16px 0; }}
    .btn-red {{ background: #ef4444; }}
    .btn-green {{ background: #22c55e; }}
    .grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 16px 0; }}
    .grid-item {{ background: #f8fafc; border-radius: 6px; padding: 12px; }}
    .footer {{ background: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; }}
    .footer p {{ color: #94a3b8; font-size: 11px; margin: 0; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <p class='header-title'>NóminaApp</p>
      <p class='header-sub'>Sistema de nómina inteligente para México</p>
    </div>
    <div class='body'>
      {contenido}
    </div>
    <div class='footer'>
      <p>Este mensaje fue generado automáticamente por NóminaApp. No responder a este correo.</p>
      <p style='margin-top:4px'>© 2025 NóminaApp · Sistema de Nómina Inteligente para México</p>
    </div>
  </div>
</body>
</html>";

    public static string ReciboTimbrado(
        string nombreEmpleado,
        string empresa,
        string periodo,
        string uuid,
        decimal neto,
        decimal isr,
        decimal imssObrero,
        string fechaPago) => Base($@"
<h2>Tu recibo de nómina está listo ✓</h2>
<p>Hola <strong>{nombreEmpleado}</strong>,</p>
<p>Tu recibo de nómina del periodo <strong>{periodo}</strong> ha sido timbrado correctamente ante el SAT.</p>

<div class='card card-green'>
  <div class='label'>Neto a pagar</div>
  <div class='value-big' style='color:#22c55e'>${neto:F2}</div>
  <div class='label' style='margin-top:8px'>Empresa: {empresa}</div>
</div>

<div class='grid'>
  <div class='grid-item'>
    <div class='label'>ISR retenido</div>
    <div class='value'>${isr:F2}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>IMSS obrero</div>
    <div class='value'>${imssObrero:F2}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Fecha de pago</div>
    <div class='value'>{fechaPago}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Folio fiscal (UUID)</div>
    <div class='value' style='font-size:11px;font-family:monospace'>{uuid.Substring(0, 18)}...</div>
  </div>
</div>

<p style='color:#94a3b8;font-size:12px'>UUID completo: <span style='font-family:monospace'>{uuid}</span></p>
<p>Puedes descargar tu recibo completo desde el portal del empleado.</p>");

    public static string AlertaCierrePeriodo(
        string nombreContador,
        string empresa,
        string periodo,
        string fechaCierre,
        int diasRestantes,
        int totalEmpleados,
        decimal costoTotal,
        string urlSistema = "http://localhost:3000") => Base($@"
<h2>⚠️ Periodo cierra en {diasRestantes} día(s)</h2>
<p>Hola <strong>{nombreContador}</strong>,</p>
<p>El siguiente periodo de nómina está próximo a cerrarse. Verifica que todo esté capturado antes de la fecha límite.</p>

<div class='card card-amber'>
  <div class='label'>Periodo</div>
  <div class='value'>{periodo}</div>
  <div class='label' style='margin-top:8px'>Fecha de cierre</div>
  <div class='value'>{fechaCierre}</div>
</div>

<div class='grid'>
  <div class='grid-item'>
    <div class='label'>Empresa</div>
    <div class='value'>{empresa}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Días restantes</div>
    <div class='value' style='color:#f59e0b'>{diasRestantes} día(s)</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Empleados activos</div>
    <div class='value'>{totalEmpleados}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Costo estimado</div>
    <div class='value'>${costoTotal:F2}</div>
  </div>
</div>

<p><strong>Checklist antes del cierre:</strong></p>
<p>✓ Incidencias capturadas (faltas, vacaciones, horas extra)<br/>
✓ Empleados con CLABE bancaria registrada<br/>
✓ SBC actualizado ante el IMSS<br/>
✓ Nómina calculada y revisada</p>

<a href='{urlSistema}/cierre' class='btn' style='background:#f59e0b'>Ir al cierre del periodo →</a>");

    public static string ErrorTimbrado(
        string nombreContador,
        string empresa,
        string empleado,
        string periodo,
        string error) => Base($@"
<h2>🚨 Error al timbrar CFDI</h2>
<p>Hola <strong>{nombreContador}</strong>,</p>
<p>Se produjo un error al intentar timbrar el CFDI de nómina del siguiente empleado:</p>

<div class='card card-red'>
  <div class='label'>Empleado</div>
  <div class='value'>{empleado}</div>
  <div class='label' style='margin-top:8px'>Empresa</div>
  <div class='value'>{empresa}</div>
  <div class='label' style='margin-top:8px'>Periodo</div>
  <div class='value'>{periodo}</div>
</div>

<div class='card card-red'>
  <div class='label'>Mensaje de error</div>
  <div class='value' style='font-size:13px;color:#dc2626'>{error}</div>
</div>

<p><strong>Posibles causas:</strong></p>
<p>• RFC del empleado con formato incorrecto<br/>
- CURP no registrado en el IMSS<br/>
- CSD vencido en Facturama<br/>
- SAT temporalmente no disponible</p>

<a href='http://localhost:3000/cola-timbrado' class='btn btn-red'>Ver cola de timbrado →</a>");

    public static string ResumenNomina(
        string nombreContador,
        string empresa,
        string periodo,
        int totalEmpleados,
        int timbrados,
        int errores,
        decimal totalNeto,
        decimal costoEmpresa) => Base($@"
<h2>Resumen de nómina procesada</h2>
<p>Hola <strong>{nombreContador}</strong>,</p>
<p>El proceso de nómina del periodo <strong>{periodo}</strong> ha concluido.</p>

<div class='grid'>
  <div class='grid-item'>
    <div class='label'>Empleados</div>
    <div class='value'>{totalEmpleados}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>CFDIs timbrados</div>
    <div class='value' style='color:#22c55e'>{timbrados}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Con errores</div>
    <div class='value' style='color:{(errores > 0 ? "#ef4444" : "#22c55e")}'>{errores}</div>
  </div>
  <div class='grid-item'>
    <div class='label'>Total neto</div>
    <div class='value'>${totalNeto:F2}</div>
  </div>
</div>

<div class='card card-green'>
  <div class='label'>Costo total empresa</div>
  <div class='value-big'>${costoEmpresa:F2}</div>
</div>

{(errores > 0 ? $"<p style='color:#dc2626'>⚠️ Hay {errores} CFDI(s) con error. Revisa la cola de timbrado para reintentarlos.</p><a href='http://localhost:3000/cola-timbrado' class='btn btn-red'>Ver errores →</a>" : "<p style='color:#16a34a'>✓ Todos los CFDIs fueron timbrados correctamente.</p>")}");

    public static string AlertaSalarioMinimo(
        string nombreContador,
        string empresa,
        List<(string nombre, decimal salario)> empleados) => Base($@"
<h2>🚨 Empleados con salario menor al mínimo</h2>
<p>Hola <strong>{nombreContador}</strong>,</p>
<p>Se detectaron empleados en <strong>{empresa}</strong> con salario inferior al mínimo vigente (<strong>$248.93/día</strong>). Esto es una violación a la LFT Art. 90.</p>

<div class='card card-red'>
{string.Join("", empleados.Select(e => $"<div style='padding:6px 0;border-bottom:1px solid #fee2e2'><strong>{e.nombre}</strong> — ${e.salario:F2}/día</div>"))}
</div>

<p>Corrige los salarios a la brevedad para evitar multas del IMSS e INFONAVIT.</p>
<a href='http://localhost:3000/empleados' class='btn btn-red'>Corregir empleados →</a>");
}