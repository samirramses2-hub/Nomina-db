namespace NominaApp.Core.Calculos;

public class ResultadoCalculo
{
    public decimal SalarioBase { get; set; }
    public decimal TotalPercepciones { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal NetoPagar { get; set; }
    public List<LineaCalculo> Percepciones { get; set; } = new();
    public List<LineaCalculo> Deducciones { get; set; } = new();
    public DetalleISR DetalleISR { get; set; } = new();
}

public class LineaCalculo
{
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Explicacion { get; set; } = string.Empty;
}

public class DetalleISR
{
    public decimal BaseGravable { get; set; }
    public decimal LimiteInferior { get; set; }
    public decimal Excedente { get; set; }
    public decimal TasaExcedente { get; set; }
    public decimal ImpuestoPrevio { get; set; }
    public decimal CuotaFija { get; set; }
    public decimal ISRCausado { get; set; }
    public decimal SubsidioEmpleo { get; set; }
    public decimal ISRRetenido { get; set; }
    public string Explicacion { get; set; } = string.Empty;
}

public class ParametrosCalculo
{
    public decimal SalarioDiario { get; set; }
    public int DiasPeriodo { get; set; }
    public int EjercicioFiscal { get; set; }

    // Incidencias
    public decimal FaltasInjustificadas { get; set; }
    public decimal FaltasJustificadas { get; set; }
    public decimal DiasVacaciones { get; set; }
    public decimal HorasExtraSimples { get; set; }
    public decimal HorasExtraDobles { get; set; }
    public decimal HorasExtraTriples { get; set; }
    public decimal Bonos { get; set; }
    public decimal DiasPrimaDominical { get; set; }
    public decimal IncapacidadIMSS { get; set; }
    public decimal IncapacidadRiesgo { get; set; }
    public decimal IncapacidadMaternidad { get; set; }
    public decimal LicenciaConGoce { get; set; }
    public decimal LicenciaSinGoce { get; set; }
    public decimal PrimaVacacional { get; set; }
    public decimal Aguinaldo { get; set; }
    public decimal DescuentoInfonavit { get; set; }
    public decimal DescuentoFonacot { get; set; }
}

public static class MotorCalculo
{
    public static ResultadoCalculo Calcular(ParametrosCalculo p)
{
    var resultado = new ResultadoCalculo();

    if (p.SalarioDiario <= 0)
        throw new ArgumentException("El salario diario debe ser mayor a cero.");

    if (p.DiasPeriodo <= 0 || p.DiasPeriodo > 31)
        throw new ArgumentException("Los días del periodo deben estar entre 1 y 31.");

    if (p.EjercicioFiscal < 2020 || p.EjercicioFiscal > 2099)
        throw new ArgumentException("El ejercicio fiscal no es válido.");

    // ── PERCEPCIONES ──────────────────────────────────────────
    var salarioQuincenal = Math.Round(p.SalarioDiario * p.DiasPeriodo, 2);
    resultado.SalarioBase = salarioQuincenal;

    resultado.Percepciones.Add(new LineaCalculo
    {
        Concepto    = "Salario base",
        Monto       = salarioQuincenal,
        Explicacion = $"${p.SalarioDiario:F2}/día × {p.DiasPeriodo} días del periodo"
    });

    if (p.HorasExtraSimples > 0)
    {
        var valorHora = Math.Round(p.SalarioDiario / 8, 2);
        var monto     = Math.Round(valorHora * 2 * p.HorasExtraSimples, 2);
        resultado.Percepciones.Add(new LineaCalculo
        {
            Concepto    = "Horas extra simples",
            Monto       = monto,
            Explicacion = $"Valor hora (${valorHora:F2}) × 2 × {p.HorasExtraSimples} horas (LFT Art. 67)"
        });
    }

    if (p.HorasExtraDobles > 0)
    {
        var valorHora = Math.Round(p.SalarioDiario / 8, 2);
        var monto     = Math.Round(valorHora * 3 * p.HorasExtraDobles, 2);
        resultado.Percepciones.Add(new LineaCalculo
        {
            Concepto    = "Horas extra dobles",
            Monto       = monto,
            Explicacion = $"Valor hora (${valorHora:F2}) × 3 × {p.HorasExtraDobles} horas (LFT Art. 68)"
        });
    }

    if (p.DiasPrimaDominical > 0)
    {
        var monto = Math.Round(p.SalarioDiario * 0.25m * p.DiasPrimaDominical, 2);
        resultado.Percepciones.Add(new LineaCalculo
        {
            Concepto    = "Prima dominical",
            Monto       = monto,
            Explicacion = $"25% del salario diario × {p.DiasPrimaDominical} domingos (LFT Art. 71)"
        });
    }

    if (p.Bonos > 0)
    {
        resultado.Percepciones.Add(new LineaCalculo
        {
            Concepto    = "Bono",
            Monto       = Math.Round(p.Bonos, 2),
            Explicacion = "Bono registrado en incidencias del periodo"
        });
    }

    if (p.PrimaVacacional > 0)
{
    // Prima vacacional: 25% del salario por días de vacaciones, exenta hasta 15 UMAs
    var montoTotal = Math.Round(p.SalarioDiario * p.PrimaVacacional * 0.25m, 2);
    var uma2024    = 108.57m;
    var exento     = Math.Round(Math.Min(montoTotal, uma2024 * 15), 2);
    var gravado    = Math.Round(montoTotal - exento, 2);
    resultado.Percepciones.Add(new LineaCalculo
    {
        Concepto    = "Prima vacacional",
        Monto       = montoTotal,
        Explicacion = $"25% × ${p.SalarioDiario:F2}/día × {p.PrimaVacacional} días vacaciones. Exento: ${exento:F2} (15 UMAs). Gravado: ${gravado:F2}"
    });
}

if (p.Aguinaldo > 0)
{
    // Aguinaldo: exento hasta 30 UMAs anuales
    var uma2024   = 108.57m;
    var exento    = Math.Round(Math.Min(p.Aguinaldo, uma2024 * 30), 2);
    var gravado   = Math.Round(p.Aguinaldo - exento, 2);
    resultado.Percepciones.Add(new LineaCalculo
    {
        Concepto    = "Aguinaldo",
        Monto       = p.Aguinaldo,
        Explicacion = $"Aguinaldo ${p.Aguinaldo:F2}. Exento: ${exento:F2} (30 UMAs). Gravado: ${gravado:F2}"
    });
}

    resultado.TotalPercepciones = Math.Round(resultado.Percepciones.Sum(x => x.Monto), 2);

    // ── DEDUCCIONES ───────────────────────────────────────────
    if (p.FaltasInjustificadas > 0)
    {
        var monto = Math.Round(p.SalarioDiario * p.FaltasInjustificadas, 2);
        resultado.Deducciones.Add(new LineaCalculo
        {
            Concepto    = "Faltas injustificadas",
            Monto       = monto,
            Explicacion = $"${p.SalarioDiario:F2}/día × {p.FaltasInjustificadas} faltas"
        });
    }

    if (p.FaltasJustificadas > 0)
    {
        var monto = Math.Round(p.SalarioDiario * p.FaltasJustificadas, 2);
        resultado.Deducciones.Add(new LineaCalculo
        {
            Concepto    = "Faltas justificadas",
            Monto       = monto,
            Explicacion = $"${p.SalarioDiario:F2}/día × {p.FaltasJustificadas} faltas"
        });
    }

    // Incapacidad IMSS — el IMSS paga el 60% a partir del 4to día, empresa descuenta los primeros 3
if (p.IncapacidadIMSS > 0)
{
    var diasDescuento = Math.Min(p.IncapacidadIMSS, 3);
    var monto = Math.Round(p.SalarioDiario * diasDescuento, 2);
    resultado.Deducciones.Add(new LineaCalculo
    {
        Concepto    = "Incapacidad IMSS (Enf. General)",
        Monto       = monto,
        Explicacion = $"Primeros {diasDescuento} días a cargo del patrón. IMSS cubre 60% desde día 4. Descuento: ${monto:F2}"
    });
}

if (p.IncapacidadRiesgo > 0)
{
    // Riesgo de trabajo: IMSS paga desde día 1, empresa no descuenta
    resultado.Percepciones.Add(new LineaCalculo
    {
        Concepto    = "Incapacidad riesgo de trabajo",
        Monto       = 0,
        Explicacion = $"{p.IncapacidadRiesgo} días. IMSS cubre 100% desde día 1 (Art. 58 LSS). Sin descuento al empleado."
    });
}

if (p.IncapacidadMaternidad > 0)
{
    resultado.Percepciones.Add(new LineaCalculo
    {
        Concepto    = "Incapacidad por maternidad",
        Monto       = 0,
        Explicacion = $"{p.IncapacidadMaternidad} días. IMSS cubre 100% del salario (84 días prenatal + 42 postnatal). Sin descuento."
    });
}

if (p.LicenciaSinGoce > 0)
{
    var monto = Math.Round(p.SalarioDiario * p.LicenciaSinGoce, 2);
    resultado.Deducciones.Add(new LineaCalculo
    {
        Concepto    = "Licencia sin goce de sueldo",
        Monto       = monto,
        Explicacion = $"${p.SalarioDiario:F2}/día × {p.LicenciaSinGoce} días sin goce"
    });
}

if (p.DescuentoInfonavit > 0)
{
    resultado.Deducciones.Add(new LineaCalculo
    {
        Concepto    = "Descuento Infonavit",
        Monto       = Math.Round(p.DescuentoInfonavit, 2),
        Explicacion = "Descuento por crédito Infonavit según tabla de VSM o factor"
    });
}

if (p.DescuentoFonacot > 0)
{
    resultado.Deducciones.Add(new LineaCalculo
    {
        Concepto    = "Descuento FONACOT",
        Monto       = Math.Round(p.DescuentoFonacot, 2),
        Explicacion = "Descuento por crédito FONACOT"
    });
}

    var imssObrero = CalcularIMSSObrero(p.SalarioDiario, p.DiasPeriodo);
    resultado.Deducciones.Add(new LineaCalculo
    {
        Concepto    = "IMSS obrero",
        Monto       = imssObrero,
        Explicacion = "Cuotas obreras: Enf/Mat 0.75% + Inv/Vida 0.625% + Cesantía 1.125% + OOMF 0.375% sobre SBC quincenal"
    });

    var baseGravable = Math.Round(resultado.TotalPercepciones - resultado.Deducciones.Sum(x => x.Monto), 2);
    if (baseGravable < 0) baseGravable = 0;

    var detalleISR = CalcularISR(baseGravable, p.EjercicioFiscal);
    resultado.DetalleISR = detalleISR;

    if (detalleISR.ISRRetenido > 0)
    {
        resultado.Deducciones.Add(new LineaCalculo
        {
            Concepto    = "ISR retenido",
            Monto       = detalleISR.ISRRetenido,
            Explicacion = detalleISR.Explicacion
        });
    }

    resultado.TotalDeducciones = Math.Round(resultado.Deducciones.Sum(x => x.Monto), 2);
    resultado.NetoPagar        = Math.Round(resultado.TotalPercepciones - resultado.TotalDeducciones, 2);

    return resultado;
}

    public static CuotasIMSS CalcularCuotasIMSS(
    decimal salarioDiario,
    int diasPeriodo,
    decimal primaVacacional = 0.25m,
    decimal aguinaldoDias = 15,
    decimal primaRiesgoTrabajo = 0.01m)
{
    // Factor de integración
    // FI = 1 + (Prima vacacional × días vacaciones / 365) + (Aguinaldo / 365)
    // Usando mínimos de ley: 6 días vacaciones año 1
    var diasVacaciones = 6m;
    var fi = 1 + (primaVacacional * diasVacaciones / 365) + (aguinaldoDias / 365);
    fi = Math.Round(fi, 4);

    var sbc = Math.Round(salarioDiario * fi, 2);

    // Tope máximo SBC = 25 UMA diarias (2024: UMA = $108.57)
    var uma2024 = 108.57m;
    var topeSBC = uma2024 * 25;
    if (sbc > topeSBC) sbc = topeSBC;

    var sbcPeriodo = Math.Round(sbc * diasPeriodo, 2);

    // ── CUOTAS OBRERAS ────────────────────────────────────────
    var emObrero   = Math.Round(sbcPeriodo * 0.0075m, 2);
    var ivObrero   = Math.Round(sbcPeriodo * 0.00625m, 2);
    var cvObrero   = Math.Round(sbcPeriodo * 0.01125m, 2);
    var oomfObrero = Math.Round(sbcPeriodo * 0.00375m, 2);
    var totalObrero = emObrero + ivObrero + cvObrero + oomfObrero;

    // ── CUOTAS PATRONALES ─────────────────────────────────────
    var emPatron   = Math.Round(sbcPeriodo * 0.204m, 2);
    var ivPatron   = Math.Round(sbcPeriodo * 0.0175m, 2);
    var guard      = Math.Round(sbcPeriodo * 0.01m, 2);
    var riesgo     = Math.Round(sbcPeriodo * primaRiesgoTrabajo, 2);
    var sar        = Math.Round(sbcPeriodo * 0.02m, 2);
    
    // Nueva tarifa progresiva Cesantía y Vejez (CV Patronal) 2024
    var sbcUmas = sbc / uma2024;
    var tasaCVPatron = 0.0315m;
    if (sbcUmas > 1.01m && sbcUmas <= 1.50m) tasaCVPatron = 0.03281m;
    else if (sbcUmas > 1.50m && sbcUmas <= 2.00m) tasaCVPatron = 0.03575m;
    else if (sbcUmas > 2.00m && sbcUmas <= 2.50m) tasaCVPatron = 0.03751m;
    else if (sbcUmas > 2.50m && sbcUmas <= 3.00m) tasaCVPatron = 0.03869m;
    else if (sbcUmas > 3.00m && sbcUmas <= 3.50m) tasaCVPatron = 0.03953m;
    else if (sbcUmas > 3.50m && sbcUmas <= 4.00m) tasaCVPatron = 0.04016m;
    else if (sbcUmas > 4.01m) tasaCVPatron = 0.04241m; // Hasta 11.875% en 2030, pero en 2024 tope ~4.241%

    var cvPatron   = Math.Round(sbcPeriodo * tasaCVPatron, 2);
    var infonavit  = Math.Round(sbcPeriodo * 0.05m, 2);
    var totalPatronal = emPatron + ivPatron + guard + riesgo + sar + cvPatron + infonavit;

    return new CuotasIMSS
    {
        SBC                          = sbc,
        FactorIntegracion            = fi,
        EnfermedadMaternidadObrero   = emObrero,
        InvalidezVidaObrero          = ivObrero,
        CesantiaVejezObrero          = cvObrero,
        OOMFObrero                   = oomfObrero,
        TotalObrero                  = totalObrero,
        EnfermedadMaternidadPatron   = emPatron,
        InvalidezVidaPatron          = ivPatron,
        GuarderiasPrestaciones       = guard,
        RiesgoTrabajo                = riesgo,
        RetiroSAR                    = sar,
        CesantiaVejezPatron          = cvPatron,
        Infonavit                    = infonavit,
        TotalPatronal                = totalPatronal,
        Explicacion                  = $"SBC = ${salarioDiario:F2}/día × FI {fi} = ${sbc:F2}/día. " +
                                       $"SBC periodo (×{diasPeriodo} días) = ${sbcPeriodo:F2}. " +
                                       $"Cuota patronal total = ${totalPatronal:F2} " +
                                       $"(EM {emPatron:F2} + IV {ivPatron:F2} + Guard {guard:F2} + " +
                                       $"Riesgo {riesgo:F2} + SAR {sar:F2} + CV {cvPatron:F2} + INFONAVIT {infonavit:F2})"
    };
}


    private static decimal CalcularIMSSObrero(decimal salarioDiario, int diasPeriodo)
{
    var sbc = Math.Round(salarioDiario * diasPeriodo, 2);
    var enfermedadMaternidad = Math.Round(sbc * 0.0075m, 2);
    var invalidezVida        = Math.Round(sbc * 0.00625m, 2);
    var cesantia             = Math.Round(sbc * 0.01125m, 2);
    var oomf                 = Math.Round(sbc * 0.00375m, 2);
    return enfermedadMaternidad + invalidezVida + cesantia + oomf;
}

    private static DetalleISR CalcularISR(decimal baseGravable, int ejercicio)
{
    var tabla    = TablaISR.ObtenerTablaQuincenal(ejercicio);
    var subsidio = TablaISR.ObtenerTablaSubsidioQuincenal(ejercicio);

    var renglon = tabla.FirstOrDefault(r =>
        baseGravable >= r.LimiteInferior && baseGravable <= r.LimiteSuperior);

    if (renglon is null)
        return new DetalleISR { Explicacion = "Base gravable fuera de tabla." };

    var excedente      = Math.Round(baseGravable - renglon.LimiteInferior, 2);
    var impuestoPrevio = Math.Round(excedente * (renglon.TasaExcedente / 100), 2);
    var isrCausado     = Math.Round(impuestoPrevio + renglon.CuotaFija, 2);

    var renglonSubsidio = subsidio.FirstOrDefault(r =>
        baseGravable >= r.LimiteInferior && baseGravable <= r.LimiteSuperior);
    var subsidioAplicable = renglonSubsidio?.SubsidioAplicable ?? 0m;

    var isrRetenido = Math.Round(isrCausado - subsidioAplicable, 2);
    if (isrRetenido < 0) isrRetenido = 0;

    return new DetalleISR
    {
        BaseGravable      = Math.Round(baseGravable, 2),
        LimiteInferior    = renglon.LimiteInferior,
        Excedente         = excedente,
        TasaExcedente     = renglon.TasaExcedente,
        ImpuestoPrevio    = impuestoPrevio,
        CuotaFija         = renglon.CuotaFija,
        ISRCausado        = isrCausado,
        SubsidioEmpleo    = subsidioAplicable,
        ISRRetenido       = isrRetenido,
        Explicacion       = $"Base ${Math.Round(baseGravable, 2):F2} cae en rango " +
                            $"${renglon.LimiteInferior:F2}-${renglon.LimiteSuperior:F2}. " +
                            $"Excedente ${excedente:F2} × {renglon.TasaExcedente}% = ${impuestoPrevio:F2} " +
                            $"+ cuota fija ${renglon.CuotaFija:F2} = ISR causado ${isrCausado:F2} " +
                            $"- subsidio al empleo ${subsidioAplicable:F2} = ISR retenido ${isrRetenido:F2}"
    };
}
public class CuotasIMSS
{
    // Obrero
    public decimal EnfermedadMaternidadObrero { get; set; }
    public decimal InvalidezVidaObrero { get; set; }
    public decimal CesantiaVejezObrero { get; set; }
    public decimal OOMFObrero { get; set; }
    public decimal TotalObrero { get; set; }

    // Patronal
    public decimal EnfermedadMaternidadPatron { get; set; }
    public decimal InvalidezVidaPatron { get; set; }
    public decimal GuarderiasPrestaciones { get; set; }
    public decimal RiesgoTrabajo { get; set; }
    public decimal RetiroSAR { get; set; }
    public decimal CesantiaVejezPatron { get; set; }
    public decimal Infonavit { get; set; }
    public decimal TotalPatronal { get; set; }

    // SBC
    public decimal SBC { get; set; }
    public decimal FactorIntegracion { get; set; }
    public string Explicacion { get; set; } = string.Empty;
}
}