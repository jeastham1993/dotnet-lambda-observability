using ObservableLambda.NativeAWS.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObservableLambda.NativeAWS
{
    public class RequestReceived : IObservable
    {
        public string Type => "request-received";
    }
}
