namespace NominaApp.Core.Calculos;

public class TablaISR
{
    public static List<RenglonISR> ObtenerTablaQuincenal(int ejercicio)
    {
        // Tabla quincenal 2024 — Anexo 8 RMF 2024
        return new List<RenglonISR>
        {
            new(0.01m,        368.10m,      0m,          1.92m),
            new(368.11m,      3124.35m,     7.05m,       6.40m),
            new(3124.36m,     5490.75m,     183.45m,     10.88m),
            new(5490.76m,     6404.10m,     441.00m,     16.00m),
            new(6404.11m,     7669.50m,     587.10m,     17.92m),
            new(7669.51m,     15446.10m,    813.90m,     21.36m),
            new(15446.11m,    24385.80m,    2475.00m,    23.52m),
            new(24385.81m,    46542.45m,    4578.00m,    30.00m),
            new(46542.46m,    62057.40m,    11225.10m,   32.00m),
            new(62057.41m,    186172.05m,   16189.80m,   34.00m),
            new(186172.06m,   decimal.MaxValue, 58388.85m, 35.00m)
        };
    }

    public static List<RenglonSubsidio> ObtenerTablaSubsidioQuincenal(int ejercicio)
    {
        // DECRETO SUBSIDIO AL EMPLEO 2024 (1 de Mayo 2024)
        // Ya no es una tabla larga, solo aplica si la base mensual es menor a $9081.00
        // En quincenal es aprox menor a $4540.50
        return new List<RenglonSubsidio>
        {
            new(0.01m,        4540.50m,     195.10m), // Subsidio tope aprox 11.82% de UMA mensual / 2
            new(4540.51m,     decimal.MaxValue, 0m)
        };
    }
}

public record RenglonISR(
    decimal LimiteInferior,
    decimal LimiteSuperior,
    decimal CuotaFija,
    decimal TasaExcedente
);

public record RenglonSubsidio(
    decimal LimiteInferior,
    decimal LimiteSuperior,
    decimal SubsidioAplicable
);