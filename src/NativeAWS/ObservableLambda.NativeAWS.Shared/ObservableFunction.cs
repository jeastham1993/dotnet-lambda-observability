using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ObservableLambda.NativeAWS.Shared
{
    public interface IObservable
    {
        string Type { get; }
    }

    public interface IObserver
    {
        Task OnNext(IObservable value);
        Task OnError(Exception error);
        Task OnCompleted();
    }

    public class ObservableFunction
    {
        private List<IObserver> _observers = new List<IObserver>();

        public ObservableFunction()
        {
            Tracing.Init();
            Logging.Init();
            this.Subscribe(new MetricObserver());
        }

        public void Subscribe(IObserver observer)
        {
            _observers.Add(observer);
        }

        public async Task Notify(IObservable observable)
        {
            foreach (var observer in _observers)
            {
                await observer.OnNext(observable);
            }
        }
    }
}