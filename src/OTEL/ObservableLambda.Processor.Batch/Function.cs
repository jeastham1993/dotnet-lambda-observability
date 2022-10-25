using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared;
using ObservableLambda.Shared.Messaging;
using ObservableLambda.Shared.ViewModel;
using OpenTelemetry.Trace;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.Processor.Batch
{
    public class Function : ObservableFunction<SQSEvent, string>
    {
        private readonly IAmazonEventBridge _eventBridgeClient;
        
        public Function() : base(new TraceOptions("ObservableLambda.Processor.Batch")
        {
            AddAwsInstrumentation = false
        })
        {
            this._eventBridgeClient = new AmazonEventBridgeClient(new EnvironmentVariablesAWSCredentials());
        }

        internal Function(IAmazonEventBridge eventBridgeClient, TraceOptions options) : base(options)
        {
            this._eventBridgeClient = eventBridgeClient;
        }

        public override Func<SQSEvent, ILambdaContext, Task<string>> Handler =>
            FunctionHandler;

        public async Task<string> FunctionHandler(SQSEvent request,
            ILambdaContext lambdaContext)
        {
            var eventBridgeEntries = new List<PutEventsRequestEntry>();
            
            foreach (var message in request.Records)
            {
                var messageContents = parseMessage(message);
                
                var context = this.HydrateContextFromSnsMessage(message);

                using (var messageProcessingSpan =
                       Activity.Current.Source.StartActivity($"ProcessingMessage{message.MessageId}",
                           ActivityKind.Consumer, this.Context, links: new List<ActivityLink>(1) {new ActivityLink(context)}))
                {
                    eventBridgeEntries.Add(new PutEventsRequestEntry()
                    {
                        Detail = JsonSerializer.Serialize(messageContents),
                        Source = "prod.user-api",
                        DetailType = "user-created",
                    });
                }
            }

            using (var eventBridgePutEventsActivity = Activity.Current.Source.StartActivity("event-bridge-put-events"))
            {
                eventBridgePutEventsActivity?.AddTag("eventbridge.total_events", eventBridgeEntries.Count.ToString());
                
                var response = await this._eventBridgeClient.PutEventsAsync(new PutEventsRequest()
                {
                    Entries = eventBridgeEntries
                });   
                
                eventBridgePutEventsActivity?.AddTag("eventbridge.failed_entry_count", response.FailedEntryCount.ToString());
                eventBridgePutEventsActivity?.AddTag("eventbridge.status_code", ((int)response.HttpStatusCode).ToString());
            }

            return "OK";
        }

        private ActivityContext HydrateContextFromSnsMessage(SQSEvent.SQSMessage message)
        {
            var wrappedMessage = parseMessage(message);

            var hydratedContext = new ActivityContext(
                ActivityTraceId.CreateFromString(wrappedMessage.Metadata.TraceParent.AsSpan()),
                ActivitySpanId.CreateFromString(wrappedMessage.Metadata.ParentSpan.AsSpan()),
                ActivityTraceFlags.Recorded);

            return hydratedContext;
        }

        private MessageWrapper<UserDTO> parseMessage(SQSEvent.SQSMessage message)
        {
            var snsData = JsonSerializer.Deserialize<SnsToSqsMessageBody>(message.Body);
            return JsonSerializer.Deserialize<MessageWrapper<UserDTO>>(snsData.Message);
        }
    }
}