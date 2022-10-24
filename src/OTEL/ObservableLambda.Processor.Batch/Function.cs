using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared;
using ObservableLambda.Shared.Messaging;
using OpenTelemetry.Trace;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.Processor.Batch
{
    public class Function : ObservableFunction<SQSEvent, string>
    {
        public Function() : base(new TraceOptions("ObservableLambda.Processor.Batch")
        {
            AddAwsInstrumentation = false
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

                using (var messageProcessingSpan =
                       Activity.Current.Source.StartActivity($"ProcessingMessage{message.MessageId}",
                           ActivityKind.Consumer, this.Context, links: new List<ActivityLink>(1) {new ActivityLink(context)}))
                {
                    await Task.Delay(5);
                }
            }

            return "OK";
        }

        private ActivityContext HydrateContextFromSnsMessage(SQSEvent.SQSMessage message)
        {
            var snsData = JsonSerializer.Deserialize<SnsToSqsMessageBody>(message.Body);
            var wrappedMessage = JsonSerializer.Deserialize<MessageWrapper<dynamic>>(snsData.Message);

            var hydratedContext = new ActivityContext(
                ActivityTraceId.CreateFromString(wrappedMessage.Metadata.TraceParent.AsSpan()),
                ActivitySpanId.CreateFromString(wrappedMessage.Metadata.ParentSpan.AsSpan()),
                ActivityTraceFlags.Recorded);

            return hydratedContext;
        }
    }
}