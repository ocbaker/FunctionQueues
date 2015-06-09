using System;
using System.Threading;

namespace FunctionQueues
{
    public class CountdownEventSub : CountdownEvent
    {
        public CountdownEvent Model { get; set; }

        public CountdownEventSub(CountdownEvent model, int initialCount) : base(initialCount)
        {
            Model = model;
        }

        public new bool Signal()
        {
            var signal = base.Signal();
            var signalModel = Model.Signal();
            return signal && signalModel;
        }

        public new bool Signal(int signalCount)
        {
            var signal = base.Signal(signalCount);
            var signalModel = Model.Signal(signalCount);
            return signal && signalModel;
        }

        public new void AddCount()
        {
            AddCount(1);
        }

        public new bool TryAddCount()
        {
            return TryAddCount(1);
        }

        public new void AddCount(int signalCount)
        {
            if (!this.TryAddCount(signalCount))
                throw new InvalidOperationException("Countdown Increment Already Zero");
        }

        public new bool TryAddCount(int signalCount)
        {
            var tryAddCount = base.TryAddCount(signalCount);
            var tryAddCountModel = Model.TryAddCount(signalCount);
            return tryAddCount && tryAddCountModel;
        }

        public new void Reset()
        {
            throw new NotSupportedException("For Sub Countdowns this is not yet supported");
        }

        public new void Reset(int count)
        {
            throw new NotSupportedException("For Sub Countdowns this is not yet supported");
        }

        //public void Wait()
        //{

        //}

        //public void Wait(CancellationToken cancellationToken)
        //{

        //}

        //public bool Wait(TimeSpan timeout)
        //{

        //}

        //public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        //{

        //}

        //public bool Wait(int millisecondsTimeout)
        //{

        //}

        //public bool Wait(int millisecondsTimeout, CancellationToken cancellationToken)
        //{

        //}
    }
}