using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NominaApp.Infrastructure.Servicios;

public class FacturamaService
{
    private readonly HttpClient _http;
    private const string SandboxUrl = "https://apisandbox.facturama.mx";

    public FacturamaService(string usuario, string password)
    {
        _http = new HttpClient { BaseAddress = new Uri(SandboxUrl) };
        var credenciales = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credenciales);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<FacturamaResponse> TimbrarNominaAsync(FacturamaCfdiNomina cfdi)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json    = JsonSerializer.Serialize(cfdi, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Console.WriteLine("=== JSON ENVIADO ===");
        Console.WriteLine(json);

        var response = await _http.PostAsync("/3/cfdis", content);
        var body     = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Status: {(int)response.StatusCode}");
        Console.WriteLine($"Response: {body}");

        if (!response.IsSuccessStatusCode)
            return new FacturamaResponse { Exito = false, Error = body };

        using var doc = JsonDocument.Parse(body);
        var root      = doc.RootElement;
        var uuid      = root.TryGetProperty("Id", out var id) ? id.GetString() ?? "" : "";

        return new FacturamaResponse { Exito = true, UUID = uuid };
    }
    public async Task<string> GetSucursalesAsync()
{
    var response = await _http.GetAsync("/api/BranchOffice");
    return await response.Content.ReadAsStringAsync();
}
}

public class FacturamaResponse
{
    public bool   Exito   { get; set; }
    public string UUID    { get; set; } = string.Empty;
    public string Error   { get; set; } = string.Empty;
}

public class FacturamaCfdiNomina
{
    public string NameId          { get; set; } = "16";
    public string ExpeditionPlace { get; set; } = "42501";
    public string CfdiType        { get; set; } = "N";
    public string PaymentMethod   { get; set; } = "PPD";
    public string  Currency        { get; set; } = "MXN";
    public string? Folio          { get; set; }
    public FacturamaReceiver   Receiver   { get; set; } = new();
    public FacturamaComplemento Complemento { get; set; } = new();
}

public class FacturamaReceiver
{
    public string Rfc          { get; set; } = string.Empty;
    public string Name         { get; set; } = string.Empty;
    public string CfdiUse      { get; set; } = "CN01";
    public string FiscalRegime { get; set; } = "605";
    public string TaxZipCode   { get; set; } = "06600";
}

public class FacturamaComplemento
{
    public FacturamaPayroll Payroll { get; set; } = new();
}

public class FacturamaPayroll
{
    public string Type                { get; set; } = "O";
    public string PaymentDate         { get; set; } = string.Empty;
    public string InitialPaymentDate  { get; set; } = string.Empty;
    public string FinalPaymentDate    { get; set; } = string.Empty;
    public int    DaysPaid            { get; set; }
    public FacturamaPayrollIssuer   Issuer      { get; set; } = new();
    public FacturamaPayrollEmployee Employee    { get; set; } = new();
    public FacturamaPerceptions     Perceptions { get; set; } = new();
    public FacturamaDeductions      Deductions  { get; set; } = new();
    public List<FacturamaOtherPayment> OtherPayments { get; set; } = new();
}

public class FacturamaPayrollIssuer
{
    public string EmployerRegistration { get; set; } = "A1234567890";

}

public class FacturamaPayrollEmployee
{
    public string Curp                    { get; set; } = string.Empty;
    public string SocialSecurityNumber    { get; set; } = string.Empty;
    public string StartDateLaborRelations { get; set; } = string.Empty;
    public string ContractType            { get; set; } = "01";
    public string RegimeType              { get; set; } = "02";
    public bool   Unionized               { get; set; } = false;
    public string TypeOfJourney           { get; set; } = "01";
    public string EmployeeNumber          { get; set; } = string.Empty;
    public string Department              { get; set; } = "General";
    public string Position                { get; set; } = "Empleado";
    public string PositionRisk            { get; set; } = "1";
    public string FrequencyPayment        { get; set; } = "04";
    public string Bank                    { get; set; } = "BANAMEX";
    public string BankAccount             { get; set; } = "1234567890123456";
    public decimal BaseSalary             { get; set; }
    public decimal DailySalary            { get; set; }
    public string FederalEntityKey        { get; set; } = "JAL";
}

public class FacturamaPerceptions
{
    public List<FacturamaPerceptionDetail> Details { get; set; } = new();
}

public class FacturamaPerceptionDetail
{
    public string  PerceptionType { get; set; } = string.Empty;
    public string  Code           { get; set; } = string.Empty;
    public string  Description    { get; set; } = string.Empty;
    public decimal TaxedAmount    { get; set; }
    public decimal ExemptAmount   { get; set; }
    public List<FacturamaExtraHours>? ExtraHours { get; set; }
}

public class FacturamaDeductions
{
    public List<FacturamaDeductionDetail> Details { get; set; } = new();
}

public class FacturamaDeductionDetail
{
    public string  DeduccionType { get; set; } = string.Empty;
    public string  Code          { get; set; } = string.Empty;
    public string  Description   { get; set; } = string.Empty;
    public decimal Amount        { get; set; }
}

public class FacturamaExtraHours
{
    public string Days      { get; set; } = "1";
    public string HoursType { get; set; } = "01";
    public string Amount    { get; set; } = string.Empty;
}

public class FacturamaOtherPayment
{
    public string  OtherPaymentType { get; set; } = string.Empty;
    public string  Code             { get; set; } = string.Empty;
    public string  Description      { get; set; } = string.Empty;
    public decimal Amount           { get; set; }
    public FacturamaEmploymentSubsidy? EmploymentSubsidy { get; set; }
}

public class FacturamaEmploymentSubsidy
{
    public decimal Amount { get; set; }
}