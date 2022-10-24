using System;
using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.SimpleSystemsManagement;
using Honeycomb.OpenTelemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Logs;

namespace ObservableLambda.Shared
{
    public abstract class ObservableFunction<TRequestType, TResponseType>
    {
        private TracerProvider _tracerProvider;
        private MeterProvider _meterProvider;
        private ActivitySource source;

        public static string SERVICE_NAME = "ObservableLambdaDemo";
        
        public ActivityContext Context;
        public ILogger<ObservableFunction<TRequestType, TResponseType>> Logger { get; private set; }
        
        public Meter Meter = new Meter(SERVICE_NAME, "1.0");
        
        public abstract Func<TRequestType, ILambdaContext, Task<TResponseType>> Handler { get; }
        
        public ObservableFunction()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(SERVICE_NAME);

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(SERVICE_NAME)
                .AddAWSInstrumentation()
                .AddAWSLambdaConfigurations()
                .AddOtlpExporter()
                .Build();
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(resourceBuilder)
                        .AddOtlpExporter();
                });
            });

            Logger = loggerFactory.CreateLogger<ObservableFunction<TRequestType, TResponseType>>();
            
            this._meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(SERVICE_NAME)
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter()
                .Build();
        }
        
        public ObservableFunction(TraceOptions options)
        {
            SERVICE_NAME = options.ServiceName;
            
            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(options.ServiceName);
            
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(options.ServiceName)
                .AddOtlpExporter()
                .AddInMemoryExporter(options.CollectedSpans)
                .Build();
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(resourceBuilder)
                        .AddOtlpExporter();
                });
            });

            Logger = loggerFactory.CreateLogger<ObservableFunction<TRequestType, TResponseType>>();;
            
            this._meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(SERVICE_NAME)
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter()
                .Build();
        }

        public async Task<TResponseType> TracedFunctionHandler(TRequestType request,
            ILambdaContext context)
        {
            if (Activity.Current == null)
            {
                source = new ActivitySource(SERVICE_NAME);
            }

            using (var rootSpan = (Activity.Current == null ? source : Activity.Current.Source).StartActivity(context.FunctionName, ActivityKind.Server, parentContext: this.Context))
            {
                rootSpan.AddTag("aws.lambda.invoked_arn", context.InvokedFunctionArn);
                rootSpan.AddTag("faas.id", context.InvokedFunctionArn);
                rootSpan.AddTag("faas.execution", context.AwsRequestId);
                rootSpan.AddTag("cloud.account.id", context.InvokedFunctionArn?.Split(":")[4]);
                rootSpan.AddTag("cloud.provider", "aws");
                rootSpan.AddTag("faas.name", context.FunctionName);
                rootSpan.AddTag("faas.version", context.FunctionVersion);

                try
                {
                    using var handlerSpan = Activity.Current.Source.StartActivity($"{context.FunctionName}_Handler");

                    TResponseType result = default;
                    Func<Task> action = async () => result = await Handler(request, context);

                    await action();

                    return result;
                }
                catch (Exception e)
                {
                    rootSpan.SetStatus(Status.Error);
                    rootSpan.RecordException(e);

                    rootSpan.Stop();

                    this._tracerProvider.ForceFlush();
                    this._meterProvider.ForceFlush();

                    throw;
                }
                finally
                {
                    rootSpan.Stop();

                    this._tracerProvider.ForceFlush();
                    this._meterProvider.ForceFlush();
                }
            }
        }
    }
}