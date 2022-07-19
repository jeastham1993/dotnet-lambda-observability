using System;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace ObservableLambda.Shared
{
    public static class Logging<TLogger>
    {
        public static ILogger<TLogger> Logger { get; private set; }

        public static void Init()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddConsoleExporter();
                    options.AddOtlpExporter();
                });
            });

            Logger = loggerFactory.CreateLogger<TLogger>();
        }
    }
}