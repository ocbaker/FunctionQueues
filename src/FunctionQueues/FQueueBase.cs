namespace FunctionQueues
{
    public abstract class FQueueBase : IFQueue
    {
        public abstract int MaxWorkers { get; }
        public virtual bool LongRunning => false;
    }
}