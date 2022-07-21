using System;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

namespace ObservableLambda.NativeAWS.Shared
{
    public class Tracing
    {
        public Tracing()
        {
            
        }

        public void BeginSegment(string name)
        {
            AWSXRayRecorder.Instance.BeginSegment(name);
        }

        public void BeginSubsegment(string name)
        {
            AWSXRayRecorder.Instance.BeginSubsegment(name);
        }

        public void EndSubsegment()
        {
            AWSXRayRecorder.Instance.EndSubsegment();
        }

        public void EndSegment()
        {
            AWSXRayRecorder.Instance.EndSegment();
        }

        public string CurrentTraceId => AWSXRayRecorder.Instance.GetEntity().TraceId;
    }
}