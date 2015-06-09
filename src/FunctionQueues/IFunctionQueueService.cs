using System;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionQueues
{
    public interface IFunctionQueueService
    {
        void AddAction<TQueue>(Func<Task> act, Action<Exception> onUnhandledException)
            where TQueue : class, IFQueue, new();
        Task AddActionAsync<TQueue>(Func<Task> act)
            where TQueue : class, IFQueue, new();

        void AddAction<TQueue>(Func<CancellationToken, Task> act, Action<Exception> onUnhandledException)
            where TQueue : class, IFQueue, new();

        Task AddActionAsync<TQueue>(Func<CancellationToken, Task> act)
            where TQueue : class, IFQueue, new();

        void AddAction<TQueue>(Action act, Action<Exception> onUnhandledException)
            where TQueue : class, IFQueue, new();
        Task AddActionAsync<TQueue>(Action act)
            where TQueue : class, IFQueue, new();

        void AddAction<TQueue>(Action<CancellationToken> act, Action<Exception> onUnhandledException)
            where TQueue : class, IFQueue, new();

        Task AddActionAsync<TQueue>(Action<CancellationToken> act)
            where TQueue : class, IFQueue, new();
    }
}