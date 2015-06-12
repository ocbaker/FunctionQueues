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
        private int _activeWorkers;

        private FQueueCollection()
        {
            Queue = new ConcurrentQueue<FQueueWork>();
        }

        public ConcurrentQueue<FQueueWork> Queue { get; private set; }
        public int ActiveWorkers { get {
                return _activeWorkers;
            } private set {
                Console.WriteLine(_activeWorkers + " -> " + value);
                _activeWorkers = value;
            } }

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
            if (ActiveWorkers >= MaxWorkers)
                return;
            lock (_lock)
            {
                ActiveWorkers++;
            }
            if (LongRunning)
                Task.Factory.StartNew(() => RunQueue(Token), TaskCreationOptions.LongRunning);
            else
                RunQueue(Token);
        }

        async Task<FQueueWork> GetItem(CancellationToken token)
        {
            FQueueWork request = null;
            while (!token.IsCancellationRequested)
            {
                if (!Queue.IsEmpty) { 
                    var tryDequeue = Queue.TryDequeue(out request);
                    if (tryDequeue)
                    {
                        return request;
                    }
                }
                await Task.Delay(1).ConfigureAwait(false);
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
                    request = await GetItem(new CancellationTokenSource(1000).Token).ConfigureAwait(false);
                    if (request == null) { 
                        lock (_lock)
                        {
                            if (ActiveWorkers == 1 && !Queue.IsEmpty && Queue.TryDequeue(out request))
                            {

                            }
                            else
                            {
                                ActiveWorkers -= 1;
                                break;
                            }
                        }
                    }
                    request.State = FQueueState.Processing;
                    try
                    {
                        await request.Act(token);
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