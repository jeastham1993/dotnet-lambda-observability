using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace ObservableLambda.Shared
{
    public static class Metrics
    {
        private static readonly Meter MyMeter = new Meter("MyCompany.MyProduct.MyLibrary", "1.0");

        public static void Init()
        {
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("MyCompany.MyProduct.MyLibrary")
                .AddConsoleExporter()
                .AddOtlpExporter()
                .Build();
        }

        public static Counter<long> CreateCounter(string counterName)
        {
            return MyMeter.CreateCounter<long>(counterName);
        }
    }
}