namespace Serverless.OpenTelemetry;

public class TraceOptions
{
    public TraceOptions(string otlpExportEndpoint = null, bool sigv4SignExport = false)
    {
        this.OtlpExportEndpoint = otlpExportEndpoint;
        SigV4SignExport = sigv4SignExport;
    }
    
    public string OtlpExportEndpoint { get; set; }
    
    public bool SigV4SignExport { get; set; }
}