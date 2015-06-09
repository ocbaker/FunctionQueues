using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionQueues
{
    public sealed class FQueueCollection
    {
        object _lock = new object();
        private Exception _internalException;

        private FQueueCollection()
        {
            Queue = new ConcurrentQueue<FQueueWork>();
        }

        public ConcurrentQueue<FQueueWork> Queue { get; private set; }
        public int ActiveWorkers { get; private set; }

        public int MaxWorkers { get; private set; }
        IFQueue QueueType { get; set; }
        CancellationToken Token { get; set; }
        public bool LongRunning { get; private set; }

        public static FQueueCollection Create<TQueue>(CancellationToken token)
            where TQueue : class, IFQueue, new()
        {
            var q = new TQueue();
            return new FQueueCollection() { MaxWorkers = q.MaxWorkers, ActiveWorkers = 0, Token = token, QueueType = q, LongRunning = q.LongRunning };
        }



        public void AddWork(FQueueWork qWork)
        {
            Queue.Enqueue(qWork);
            TryStartNewWorker();
        }

        void TryStartNewWorker()
        {
            lock (_lock)
            {
                if (ActiveWorkers >= MaxWorkers)
                    return;
                ActiveWorkers++;
                if (LongRunning)
                    Task.Factory.StartNew(() => RunQueue(Token), TaskCreationOptions.LongRunning);
                else
                    RunQueue(Token);
            }
        }

        FQueueWork GetItem(CancellationToken token)
        {
            FQueueWork request = null;
            while (!token.IsCancellationRequested)
            {
                var tryDequeue = Queue.TryDequeue(out request);
                if (tryDequeue)
                {
                    return request;
                }
            }
            return null;
        }

        async Task RunQueue(CancellationToken token)
        {
            FQueueWork request = null;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    request = GetItem(new CancellationTokenSource(100).Token);
                    lock (_lock)
                    {
                        if (request == null)
                        {
                            ActiveWorkers -= 1;
                            break;
                        }
                    }
                    request.State = FQueueState.Processing;
                    try
                    {
                        await request.Act(token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        request.OnUnhandledException(ex);
                    }
                    finally
                    {
                        request.State = FQueueState.Finished;
                    }
                }
            }
            catch (Exception ex)
            {
                var fex = new FQueueFatalException(ex);
                _internalException = fex;

                if (request != null)
                {
                    request.OnUnhandledException(ex);
                }
            }

        }
    }

    public sealed class FQueueFatalException : Exception
    {
        public FQueueFatalException(Exception ex)
            : base("The Queue has experienced a fatal exception and can not continue", ex)
        { }
    }
}