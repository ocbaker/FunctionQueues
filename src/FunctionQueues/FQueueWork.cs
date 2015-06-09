using System;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionQueues
{
    public sealed class FQueueWork
    {
        private readonly CancellationToken _token;
        public Func<CancellationToken, Task> Act { get; private set; }
        public Action<Exception> OnUnhandledException { get; private set; }
        public Task CompletionTask { get; private set; }
        public FQueueState State { get; set; }
        public Exception ThrowEx { get; set; }

        private FQueueWork(Func<CancellationToken, Task> act)
        {
            Act = act;
            State = FQueueState.Waiting;
        }
        public FQueueWork(Func<CancellationToken, Task> act, CancellationToken token) : this(act)
        {
            _token = token;
            OnUnhandledException = ex => ThrowEx = ex;
            CompletionTask = WaitForCompletion();
        }

        public FQueueWork(Func<CancellationToken, Task> act, Action<Exception> onUnhandledException) : this(act)
        {
            OnUnhandledException = onUnhandledException;
        }

        private async Task WaitForCompletion()
        {
            while (State != FQueueState.Finished)
            {
                if (State == FQueueState.Waiting && _token.IsCancellationRequested)
                    throw new TaskCanceledException();
                if (ThrowEx != null)
                    throw ThrowEx;
                await Task.Delay(100, _token).ConfigureAwait(false);
            }
        }
    }
}