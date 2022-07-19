using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ObservableLambda.NativeAWS.Shared
{
    public class MetricObserver : IObserver
    {
        private AmazonCloudWatchClient _cloudWatchClient;

        public MetricObserver()
        {
            this._cloudWatchClient = new AmazonCloudWatchClient();
        }

        public Task OnCompleted()
        {
            throw new NotImplementedException();
        }

        public Task OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public async Task OnNext(IObservable value)
        {
            Logging.Info("Storing metric");

            var datums = new List<MetricDatum>();

            switch (value.Type)
            {
                case "request-received":
                    datums.Add(new MetricDatum
                        {
                            TimestampUtc = DateTime.UtcNow,
                            MetricName = "RequestReceived",
                            Value = 1,
                            Unit = StandardUnit.Count,
                            StorageResolution = 1,
                        }
                    );
                    break;
            }

            if (!datums.Any())
            {
                return;
            }

            var request = new PutMetricDataRequest
            {
                Namespace = "observable-lambda",
                MetricData = datums
            };

            await this._cloudWatchClient.PutMetricDataAsync(request);
        }
    }
}