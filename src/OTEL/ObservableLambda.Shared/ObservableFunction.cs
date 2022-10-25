using System;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace ObservableLambda.Shared
{
    public abstract class ObservableFunction<TRequestType, TResponseType>
    {
        private TracerProvider _tracerProvider;
        private TraceOptions _options;

        public static string SERVICE_NAME = "ObservableLambdaDemo";
        
        public ActivityContext Context;
        
        public ActivitySource Source { get; private set; }

        public abstract Func<TRequestType, ILambdaContext, Task<TResponseType>> Handler { get; }
        
        public ObservableFunction(TraceOptions options)
        {
            _options = options;
            
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            if (!string.IsNullOrEmpty(options.ServiceName))
            {
                SERVICE_NAME = options.ServiceName;   
            }

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(SERVICE_NAME);

            var traceConfig = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(SERVICE_NAME)
                .AddOtlpExporter();

            if (options.AddAwsInstrumentation)
            {
                traceConfig.AddAWSInstrumentation();
            }

            if (options.AddLambdaConfiguration)
            {
                traceConfig.AddAWSLambdaConfigurations();
            }

            if (options.CollectedSpans != null)
            {
                traceConfig.AddInMemoryExporter(options.CollectedSpans);
            }

            _tracerProvider = traceConfig.Build();
        }

        public async Task<TResponseType> TracedFunctionHandler(TRequestType request,
            ILambdaContext context)
        {
            if (Activity.Current == null)
            {
                Source = new ActivitySource(SERVICE_NAME);
            }

            if (this._options.AutoStartTrace)
            {
                using (var rootSpan = (Activity.Current == null ? Source : Activity.Current.Source).StartActivity(context.FunctionName, ActivityKind.Server, parentContext: this.Context))
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
                        using (var handlerSpan =
                               Activity.Current.Source.StartActivity($"{context.FunctionName}_Handler"))
                        {
                            TResponseType result = default;
                            Func<Task> action = async () => result = await Handler(request, context);

                            await action();

                            return result;   
                        }
                    }
                    catch (Exception e)
                    {
                        rootSpan.SetStatus(Status.Error);
                        rootSpan.RecordException(e);

                        rootSpan.Stop();

                        this._tracerProvider.ForceFlush();

                        throw;
                    }
                    finally
                    {
                        rootSpan.Stop();

                        this._tracerProvider.ForceFlush();
                    }
                }
            }
            else
            {
                try
                {
                    TResponseType result = default;
                    Func<Task> action = async () => result = await Handler(request, context);

                    await action();

                    return result;
                }
                catch (Exception)
                {
                    this._tracerProvider.ForceFlush();

                    throw;
                }
                finally
                {
                    this._tracerProvider.ForceFlush();
                }
            }
        }
    }
}