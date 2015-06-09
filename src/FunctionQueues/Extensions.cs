using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionQueues
{
    public static class Extensions
    {
        public static async Task ProcessWorkAsync<TQueue, TObject>(this FunctionQueueService functionQueue, IEnumerable<TObject> work, Func<TObject, Task> workFunc, Func<CountdownEvent, Task> onTick)
            where TQueue : class, IFQueue, new()
        {
            Exception exception = null;
            var latch = new CountdownEvent(work.Count());
            
            await Task.WhenAll(DoForEachWorkItem(work, (o) =>
            {
                functionQueue.AddAction<TQueue>(async () =>
                {
                    await workFunc(o).ConfigureAwait(false);
                    latch.Signal();
                }, ex => exception = ex);
                return Task.FromResult<object>(null);
            }), Ticker(async (l) =>
            {
                if (exception != null)
                    throw exception;
                await onTick(l);
            }, latch));
        }

        public static Task ProcessWorkAsync<TObject>(this IEnumerable<TObject> work, Action<TObject> workFunc,
            Action<CountdownEvent> onTick)
        {
            return ProcessWorkAsync(work, (x) => Task.Run(() => workFunc(x)), (x) => Task.Run(() => onTick(x)));
        }

        public static async Task ProcessWorkAsync<TObject>(this IEnumerable<TObject> work, Func<TObject, Task> workFunc, Func<CountdownEvent, Task> onTick)
        {
            var latch = new CountdownEvent(work.Count());
            var i = work.Count();
            
            await Task.WhenAll(DoForEachWorkItem(work, async (o) =>
            {
                await workFunc(o).ConfigureAwait(false);
                latch.Signal();
            }), Ticker(onTick, latch));
        }

        private static async Task Ticker(Func<CountdownEvent, Task> onTick, CountdownEvent latch)
        {
            while (latch.CurrentCount != 0)
            {
                await onTick(latch).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public static async Task ProcessBatchWorkAsync<TObject>(this IEnumerable<TObject> work, Func<IEnumerable<TObject>, CountdownEventSub, Task> workFunc, Func<CountdownEvent, Task> onTick, int maxBatchSize)
        {
            var items = new List<List<TObject>>();
            var i = 0;
            items.Add(new List<TObject>());
            foreach (var o in work)
            {
                if (i == maxBatchSize)
                {
                    i = 0;
                    items.Add(new List<TObject>());
                }
                items.Last().Add(o);
                i++;
            }

            var latch = new CountdownEvent(work.Count());
            
            await Task.WhenAll(DoForEachWorkItem(items, async(batch) =>
            {
                using (var iLatch = new CountdownEventSub(latch, batch.Count))
                {
                    await workFunc(batch, iLatch).ConfigureAwait(false);
                    if (iLatch.CurrentCount != 0)
                        throw new InvalidOperationException("A batch failed to process all of its work");
                    await Task.Delay(1).ConfigureAwait(false);
                }
            }), Ticker(onTick, latch));
        }

        private static async Task DoForEachWorkItem<TObject>(IEnumerable<TObject> work, Func<TObject, Task> workFunc)
        {
            foreach (var o in work)
            {
                await workFunc(o).ConfigureAwait(false);
                await Task.Delay(1).ConfigureAwait(false);
            }
        }
        
    }
}