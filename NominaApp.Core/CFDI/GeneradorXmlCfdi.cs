using System.Text;
using System.Xml;

namespace NominaApp.Core.CFDI;

public static class GeneradorXmlCfdi
{
    public static string GenerarXml(CfdiNominaRequest req)
    {
        var totalPercepciones = req.Percepciones.Sum(p => p.ImporteGravado + p.ImporteExento);
        var totalDeducciones  = req.Deducciones.Sum(d => d.Importe);
        var totalNeto         = Math.Round(totalPercepciones - totalDeducciones, 2);

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(sb, settings);

        // ── Comprobante ──────────────────────────────────────────
        writer.WriteStartElement("cfdi", "Comprobante", "http://www.sat.gob.mx/cfd/4");
        writer.WriteAttributeString("xmlns", "nomina12", null, "http://www.sat.gob.mx/nomina12");
        writer.WriteAttributeString("Version", "4.0");
        writer.WriteAttributeString("Fecha", req.FechaPago.ToString("yyyy-MM-ddTHH:mm:ss"));
        writer.WriteAttributeString("Sello", "");
        writer.WriteAttributeString("FormaPago", "99");
        writer.WriteAttributeString("NoCertificado", "");
        writer.WriteAttributeString("Certificado", "");
        writer.WriteAttributeString("SubTotal", totalPercepciones.ToString("F2"));
        writer.WriteAttributeString("Descuento", totalDeducciones.ToString("F2"));
        writer.WriteAttributeString("Moneda", "MXN");
        writer.WriteAttributeString("Total", totalNeto.ToString("F2"));
        writer.WriteAttributeString("TipoDeComprobante", "N");
        writer.WriteAttributeString("Exportacion", "01");
        writer.WriteAttributeString("MetodoPago", "PPD");
        writer.WriteAttributeString("LugarExpedicion", "06600");

        // ── Emisor ────────────────────────────────────────────────
        writer.WriteStartElement("cfdi", "Emisor", null);
        writer.WriteAttributeString("Rfc", req.RfcEmisor);
        writer.WriteAttributeString("Nombre", req.NombreEmisor);
        writer.WriteAttributeString("RegimenFiscal", req.RegimenFiscalEmisor);
        writer.WriteEndElement();

        // ── Receptor ──────────────────────────────────────────────
        writer.WriteStartElement("cfdi", "Receptor", null);
        writer.WriteAttributeString("Rfc", req.RfcReceptor);
        writer.WriteAttributeString("Nombre", req.NombreReceptor);
        writer.WriteAttributeString("DomicilioFiscalReceptor", "06600");
        writer.WriteAttributeString("RegimenFiscalReceptor", "605");
        writer.WriteAttributeString("UsoCFDI", "CN01");
        writer.WriteEndElement();

        // ── Conceptos ─────────────────────────────────────────────
        writer.WriteStartElement("cfdi", "Conceptos", null);
        writer.WriteStartElement("cfdi", "Concepto", null);
        writer.WriteAttributeString("ClaveProdServ", "84111505");
        writer.WriteAttributeString("Cantidad", "1");
        writer.WriteAttributeString("ClaveUnidad", "ACT");
        writer.WriteAttributeString("Descripcion", "Pago de nómina");
        writer.WriteAttributeString("ValorUnitario", totalPercepciones.ToString("F2"));
        writer.WriteAttributeString("Importe", totalPercepciones.ToString("F2"));
        writer.WriteAttributeString("Descuento", totalDeducciones.ToString("F2"));
        writer.WriteAttributeString("ObjetoImp", "02");
        writer.WriteEndElement();
        writer.WriteEndElement();

        // ── Complemento Nómina 1.2 ────────────────────────────────
        writer.WriteStartElement("cfdi", "Complemento", null);
        writer.WriteStartElement("nomina12", "Nomina", null);
        writer.WriteAttributeString("TipoNomina", "O");
        writer.WriteAttributeString("FechaPago", req.FechaPago.ToString("yyyy-MM-dd"));
        writer.WriteAttributeString("FechaInicialPago", req.FechaInicialPago.ToString("yyyy-MM-dd"));
        writer.WriteAttributeString("FechaFinalPago", req.FechaFinalPago.ToString("yyyy-MM-dd"));
        writer.WriteAttributeString("NumDiasPagados", req.NumDiasPagados.ToString());
        writer.WriteAttributeString("TotalPercepciones", totalPercepciones.ToString("F2"));
        writer.WriteAttributeString("TotalDeducciones", totalDeducciones.ToString("F2"));

        // Emisor nómina
        writer.WriteStartElement("nomina12", "Emisor", null);
        writer.WriteAttributeString("RegistroPatronal", "A1234567890");
        writer.WriteEndElement();

        // Receptor nómina
        writer.WriteStartElement("nomina12", "Receptor", null);
        writer.WriteAttributeString("Curp", req.CurpReceptor);
        writer.WriteAttributeString("NumSeguridadSocial", req.NumSeguridadSocial);
        writer.WriteAttributeString("FechaInicioRelLaboral", req.FechaInicioRelLaboral.ToString("yyyy-MM-dd"));
        writer.WriteAttributeString("Antigüedad", "P1Y");
        writer.WriteAttributeString("TipoContrato", req.TipoContrato);
        writer.WriteAttributeString("Sindicalizado", "No");
        writer.WriteAttributeString("TipoJornada", "01");
        writer.WriteAttributeString("TipoRegimen", req.TipoRegimen);
        writer.WriteAttributeString("NumEmpleado", req.NumEmpleado.ToString());
        writer.WriteAttributeString("PeriodicidadPago", req.PeriodicidadPago);
        writer.WriteAttributeString("SalarioBaseCotApor", req.SalarioBaseCotApor.ToString("F2"));
        writer.WriteAttributeString("SalarioDiarioIntegrado", req.SalarioDiarioIntegrado.ToString("F2"));
        writer.WriteAttributeString("ClaveEntFed", "CDMX");
        writer.WriteEndElement();

        // Percepciones
        writer.WriteStartElement("nomina12", "Percepciones", null);
        writer.WriteAttributeString("TotalGravado", req.Percepciones.Sum(p => p.ImporteGravado).ToString("F2"));
        writer.WriteAttributeString("TotalExento", req.Percepciones.Sum(p => p.ImporteExento).ToString("F2"));

        foreach (var p in req.Percepciones)
        {
            writer.WriteStartElement("nomina12", "Percepcion", null);
            writer.WriteAttributeString("TipoPercepcion", p.TipoPercepcion);
            writer.WriteAttributeString("Clave", p.Clave);
            writer.WriteAttributeString("Concepto", p.Concepto);
            writer.WriteAttributeString("ImporteGravado", p.ImporteGravado.ToString("F2"));
            writer.WriteAttributeString("ImporteExento", p.ImporteExento.ToString("F2"));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        // Deducciones
        writer.WriteStartElement("nomina12", "Deducciones", null);
        writer.WriteAttributeString("TotalOtrosDeducciones", "0.00");
        writer.WriteAttributeString("TotalImpuestosRetenidos", req.Deducciones.Where(d => d.TipoDeduccion == "002").Sum(d => d.Importe).ToString("F2"));

        foreach (var d in req.Deducciones)
        {
            writer.WriteStartElement("nomina12", "Deduccion", null);
            writer.WriteAttributeString("TipoDeduccion", d.TipoDeduccion);
            writer.WriteAttributeString("Clave", d.Clave);
            writer.WriteAttributeString("Concepto", d.Concepto);
            writer.WriteAttributeString("Importe", d.Importe.ToString("F2"));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteEndElement(); // Nomina
        writer.WriteEndElement(); // Complemento
        writer.WriteEndElement(); // Comprobante
        writer.Flush();

        return sb.ToString();
    }
}