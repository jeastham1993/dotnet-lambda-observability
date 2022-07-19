using System;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

namespace ObservableLambda.NativeAWS.Shared
{
    public static class Tracing
    {        
        public static void Init()
        {
            AWSXRayRecorder recorder = new AWSXRayRecorderBuilder().Build();
            AWSXRayRecorder.InitializeInstance(recorder: recorder);

            AWSSDKHandler.RegisterXRayForAllServices();
        }

        public static string CurrentTraceId => AWSXRayRecorder.Instance.GetEntity().TraceId;
    }
}