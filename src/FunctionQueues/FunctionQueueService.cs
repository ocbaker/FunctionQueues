using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionQueues
{
    public class FunctionQueueService : IFunctionQueueService, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private new readonly ConcurrentDictionary<Type, FQueueCollection> _queues =
            new ConcurrentDictionary<Type, FQueueCollection>();


        public FunctionQueueService()
        {

        }

        public void AddAction<TQueue>(Func<Task> act, Action<Exception> onUnhandledException) where TQueue : class, IFQueue, new()
        {
            AddAction<TQueue>((token) => act(), onUnhandledException);
        }

        public Task AddActionAsync<TQueue>(Func<Task> act)
            where TQueue : class, IFQueue, new()
        {
            return AddActionAsync<TQueue>((token) => act());
        }

        public void AddAction<TQueue>(Func<CancellationToken, Task> act, Action<Exception> onUnhandledException) where TQueue : class, IFQueue, new()
        {
            GetQueue<TQueue>().AddWork(new FQueueWork(act, onUnhandledException));
        }

        public Task AddActionAsync<TQueue>(Func<CancellationToken, Task> act)
            where TQueue : class, IFQueue, new()
        {
            var qWork = new FQueueWork(act, _cancellationTokenSource.Token);
            GetQueue<TQueue>().AddWork(qWork);
            return qWork.CompletionTask;
        }

        public void AddAction<TQueue>(Action act, Action<Exception> onUnhandledException) where TQueue : class, IFQueue, new()
        {
            AddAction<TQueue>(() => Task.Run(act), onUnhandledException);
        }

        public Task AddActionAsync<TQueue>(Action act) where TQueue : class, IFQueue, new()
        {
            return AddActionAsync<TQueue>((token) => act());
        }

        public void AddAction<TQueue>(Action<CancellationToken> act, Action<Exception> onUnhandledException) where TQueue : class, IFQueue, new()
        {
            AddAction<TQueue>((token) => Task.Run(() => act(token)), onUnhandledException);
        }

        public Task AddActionAsync<TQueue>(Action<CancellationToken> act) where TQueue : class, IFQueue, new()
        {
            return AddActionAsync<TQueue>((token) => Task.Run(() => act(token)));
        }

        internal FQueueCollection GetQueue<TQueue>()
            where TQueue : class, IFQueue, new()
        {
            return _queues.GetOrAdd(typeof (TQueue),
                (type) => FQueueCollection.Create<TQueue>(_cancellationTokenSource.Token));
        }
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}