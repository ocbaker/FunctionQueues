using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FunctionQueues;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class FunctionQueueTests
    {
        private FunctionQueueService _fQueueService;

        [SetUp]
        public void SetUp()
        {
            _fQueueService = new FunctionQueueService();
        }

        [Test]
        public async Task CanQueueOneItemOnOneWorkerAsync()
        {
            var resulted = false;
            await _fQueueService.AddActionAsync<FQueueSingleWorker>(() => Task.Run(() => resulted = true));
            resulted.Should().BeTrue();
        }

        [Test]
        public async Task CanQueueOneItemOnOneWorkerSyncUsingTask()
        {
            var resulted = false;
            _fQueueService.AddAction<FQueueSingleWorker>(() => Task.Run(() => resulted = true), ex => ex.Should().BeNull());
            await Task.Delay(100);
            resulted.Should().BeTrue();
        }
        [Test]
        public async Task CanQueueOneItemOnOneWorkerSyncUsingAction()
        {
            var resulted = false;
            _fQueueService.AddAction<FQueueSingleWorker>(() => resulted = true, ex => ex.Should().BeNull());
            await Task.Delay(100);
            resulted.Should().BeTrue();
        }

        [Test]
        public async Task CanQueueTenItemsOnOneWorkerAsync()
        {
            var lck = new object();
            var count = 0;
            Func<Task> work = (async () =>
            {
                await Task.Delay(new Random().Next(1000));
                lock (lck)
                {
                    count++;
                }
            });
            await Task.WhenAll(
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work),
                _fQueueService.AddActionAsync<FQueueSingleWorker>(work)
                );

            count.Should().Be(10);
        }

        [Test]
        public async Task CanQueueTenItemsOnTwoWorkersAsync()
        {
            var lck = new object();
            var count = 0;
            var delay = 500;
            var sw = new Stopwatch();
            sw.Start();
            Func<Task> work = (async () =>
            {
                await Task.Delay(delay);
                lock (lck)
                {
                    count++;
                }
            });
            await Task.WhenAll(
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work),
                _fQueueService.AddActionAsync<FQueueTwoWorkers>(work)
                );
            sw.Stop();
            count.Should().Be(10);
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
            Console.WriteLine("Elapsed Time: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Min Elasable Time: {0}", delay*5);
            Console.WriteLine("Max Elasable Time: {0}", delay * 10);
        }
        [Test]
        public async Task CanQueueOneHundredItemsOnTwoWorkersAsync()
        {
            var lck = new object();
            var count = 0;
            var delay = 500;
            Func<Task> work = (async () =>
            {
                //await Task.Delay(delay);
                lock (lck)
                {
                    count++;
                }
            });

            var sw = new Stopwatch();
            sw.Start();
            var items = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(_fQueueService.AddActionAsync<FQueueTwoWorkers>(work));
            }

            await Task.WhenAll(
                items
                );
            sw.Stop();
            count.Should().Be(100);
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
            Console.WriteLine("Elapsed Time: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Min Elasable Time: {0}", delay * 5);
            Console.WriteLine("Max Elasable Time: {0}", delay * 10);
        }

        [Test]
        public async Task CanBatchItems()
        {
            var lck = new object();
            var items = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(i);
            }
            var cd = new CountdownEvent(100);
            await items.ProcessBatchWorkAsync(async (ints, @event) =>
            {
                foreach (var i in ints)
                {
                    lock (lck)
                    {
                        cd.Signal();
                        @event.Signal();
                    }
                }
            },
                async @event =>
                {
                    Console.WriteLine(@event.InitialCount + " " + @event.CurrentCount);
                    lock (lck)
                    {
                        @event.CurrentCount.Should().Be(cd.CurrentCount);
                    }
                }, 10);
            cd.CurrentCount.Should().Be(0);
        }

        [Test]
        public async Task CanUseQueueExtensionToQueueWorkAsync()
        {
            var lck = new object();
            var items = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(i);
            }
            var cd = new CountdownEvent(100);
            await DoTimedWork(async () =>
            {
                await _fQueueService.ProcessWorkAsync<FQueueTwoWorkers, int>(items, async i =>
                {
                    cd.Signal();
                }, async @event =>
                {
                    Console.WriteLine(@event.InitialCount + " " + @event.CurrentCount);
                    @event.CurrentCount.Should().BeInRange(cd.CurrentCount - 2, cd.CurrentCount + 2);
                });
            });
            
            cd.CurrentCount.Should().Be(0);
        }
        [Test]
        public async Task CanUseQueueExtensionToQueueVoidWorkAsync()
        {
            var lck = new object();
            var items = new List<int>();
            var noWork = 100000;
            for (int i = 0; i < noWork; i++)
            {
                items.Add(i);
            }
            var cd = new CountdownEvent(noWork);
            await DoTimedWork(async () =>
            {
                await _fQueueService.ProcessWorkAsync<FQueueFourWorkers, int>(items, i =>
                {
                    cd.Signal();
                    //Console.WriteLine(cd.CurrentCount);
                }, @event =>
                {
                    Console.WriteLine(@event.InitialCount + " " + @event.CurrentCount);
                    //@event.CurrentCount.Should().BeInRange(cd.CurrentCount - 2, cd.CurrentCount + 2);
                });
            });

            cd.CurrentCount.Should().Be(0);
        }

        [Test]
        public async Task CanQueueTenItemsOnTwoWorkersSync()
        {
            var lck = new object();
            var count = 0;
            var delay = 500;
            Func<Task> work = (async () =>
            {
                await Task.Delay(delay);
                lock (lck)
                {
                    count++;
                }
            });
            var sw = await DoTimedWork(async (sw1) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _fQueueService.AddAction<FQueueTwoWorkers>(work, ex => ex.Should().BeNull());
                }
                while (count < 10)
                {
                    sw1.ElapsedMilliseconds.Should().BeLessThan(5000);
                    await Task.Delay(10);
                }
            });
            Console.WriteLine("Min Elasable Time: {0}", delay * 5);
            Console.WriteLine("Max Elasable Time: {0}", delay * 10);
            count.Should().Be(10);
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
        }

        async Task<Stopwatch> DoTimedWork(Func<Task> workFunc, bool printElapsedTime = true)
        {
            var sw = new Stopwatch();
            sw.Start();
            await workFunc();
            sw.Stop();
            if(printElapsedTime)
                Console.WriteLine("Elapsed Time: {0}", sw.ElapsedMilliseconds);
            return sw;
        }

        async Task<Stopwatch> DoTimedWork(Func<Stopwatch, Task> workFunc, bool printElapsedTime = true)
        {
            var sw = new Stopwatch();
            sw.Start();
            await workFunc(sw);
            sw.Stop();
            if (printElapsedTime)
                Console.WriteLine("Elapsed Time: {0}", sw.ElapsedMilliseconds);
            return sw;
        }
    }

    internal class FQueueSingleWorker : FQueueBase
    {
        public override int MaxWorkers => 1;
    }
    internal class FQueueTwoWorkers : FQueueBase
    {
        public override int MaxWorkers => 2;
    }
    internal class FQueueFourWorkers : FQueueBase
    {
        public override int MaxWorkers => 4;
    }
}
