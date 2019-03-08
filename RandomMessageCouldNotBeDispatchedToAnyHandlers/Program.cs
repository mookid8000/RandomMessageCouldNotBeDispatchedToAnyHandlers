using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using Autofac;
using Rebus.Bus.Advanced;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Transport.InMem;
using Serilog;
using Serilog.Events;
#pragma warning disable 1998

namespace RandomMessageCouldNotBeDispatchedToAnyHandlers
{
    class Program
    {
        static void Main()
        {
            // we're looking for MessageCouldNotBeDispatchedToAnyHandlersException

            Log.Logger = new LoggerConfiguration()
                //.WriteTo.ColoredConsole(restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.RollingFile(@"C:\logs\RandomMessageCouldNotBeDispatchedToAnyHandlers\log.txt", restrictedToMinimumLevel: LogEventLevel.Information)
                .MinimumLevel.Information()
                .CreateLogger();

            var builder = new ContainerBuilder();

            builder.RegisterHandler<SimpleStringHandler>();

            builder.RegisterType<StatsCollector>().SingleInstance();

            builder.RegisterRebus(configure => configure
                .Logging(l => l.Serilog())
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "test-queue"))
                .Options(o =>
                {
                    o.SetNumberOfWorkers(4);
                    o.SetMaxParallelism(30);
                })
            );

            using (var container = builder.Build())
            using (var statsTimer = new Timer(10000))
            using (var sendTimer = new Timer(100))
            {
                statsTimer.Elapsed += (o, ea) => container.Resolve<StatsCollector>().PrintStats();
                sendTimer.Elapsed += (o, ea) => SendSomeMessages(container.Resolve<ISyncBus>());

                statsTimer.Start();
                sendTimer.Start();

                Console.WriteLine("Press ENTER to quit");
                Console.ReadLine();
            }
        }

        static readonly ThreadLocal<Random> RandomFactory = new ThreadLocal<Random>(() => new Random(DateTime.Now.GetHashCode()));

        static void SendSomeMessages(ISyncBus bus)
        {
            var random = RandomFactory.Value;

            foreach (var str in Enumerable.Range(0, random.Next(1000)).Select(n => $"THIS IS MESSAGE {n % 13}"))
            {
                bus.SendLocal(str);
            }
        }
    }

    public class SimpleStringHandler : IHandleMessages<string>
    {
        static readonly ThreadLocal<Random> RandomFactory = new ThreadLocal<Random>(() => new Random(DateTime.Now.GetHashCode()));

        readonly StatsCollector _statsCollector;

        public SimpleStringHandler(StatsCollector statsCollector) => _statsCollector = statsCollector;

        public async Task Handle(string message)
        {
            if (RandomFactory.Value.Next(1000) == 3)
            {
                throw new ArgumentException("OMG THE RANDOM VALUE IS 3!");
            }

            _statsCollector.Register(message);
        }
    }

    public class StatsCollector
    {
        static readonly ILogger Logger = Log.ForContext<StatsCollector>();
        readonly ConcurrentDictionary<string, int> _counters = new ConcurrentDictionary<string, int>();

        public void Register(string str) => _counters.AddOrUpdate(str, 1, (_, value) => value + 1);

        public void PrintStats()
        {
            var stats = _counters.Select(kvp => new { String = kvp.Key, Count = kvp.Value }).ToList();

            Logger.Information("Here's the collected stats (total = {total}): {@stats}", stats.Sum(s => s.Count),
                stats.OrderByDescending(s => s.Count));
        }
    }
}
