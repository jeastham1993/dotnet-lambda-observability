using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared;
using ObservableLambda.Shared.Messaging;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.Processor.SingleMessage
{
    public class Function : ObservableFunction<SQSEvent, string>
    {
        public Function() : base(new TraceOptions("ObservableLambda.Processor.Batch")
        {
            AddAwsInstrumentation = false,
            AutoStartTrace = false
        })
        {
        }

        internal Function(TraceOptions options) : base(options)
        {
        }

        public override Func<SQSEvent, ILambdaContext, Task<string>> Handler =>
            FunctionHandler;

        public async Task<string> FunctionHandler(SQSEvent request,
            ILambdaContext lambdaContext)
        {
            foreach (var message in request.Records)
            {
                var context = this.HydrateContextFromSnsMessage(message);
                
                using (var rootSpan = this.Source.StartActivity(lambdaContext.FunctionName, ActivityKind.Server, parentContext: context))
                {
                    rootSpan.AddTag("messaging.message_id", message.MessageId);

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            return "OK";
        }
        
        private ActivityContext HydrateContextFromSnsMessage(SQSEvent.SQSMessage message)
        {
            var snsData = JsonSerializer.Deserialize<SnsToSqsMessageBody>(message.Body);
            var wrappedMessage = JsonSerializer.Deserialize<MessageWrapper<dynamic>>(snsData.Message);
        
            var hydratedContext = new ActivityContext(ActivityTraceId.CreateFromString(wrappedMessage.Metadata.TraceParent.AsSpan()),
                ActivitySpanId.CreateFromString(wrappedMessage.Metadata.ParentSpan.AsSpan()), ActivityTraceFlags.Recorded);

            return hydratedContext;
        }
    }
}