using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

namespace ObservableLambda.NativeAWS.Shared
{
    public static class Logging
    {
        static string _traceId;

        public static void Init()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateLogger();
        }

        public static void AddTraceToLogContext(string traceId) => _traceId = traceId;

        public static void Info(string message)
        {
            Log.ForContext("TraceId", _traceId).Information(message);
        }
    }
}