namespace FunctionQueues
{
    public interface IFQueue
    {
        int MaxWorkers { get; }
        bool LongRunning { get; }
    }
}