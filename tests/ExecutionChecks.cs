using System.IO;
using System.Windows.Media.Imaging;
using RobotVisionSortingTwin;

internal static class ExecutionChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new Exception("执行联动检查失败：" + name);
            }

            checks++;
        }

        var simulation = new SortingSimulation((kind, _, _) =>
            new TwinInspection(kind, "仿真件", true, 100, 40, null, "测试"));
        simulation.Reset(true);
        var product = Guid.NewGuid();
        var controller = new ExecutionController(simulation, id => id == product ? TwinProductKind.Disc : null);
        var received = new List<ExecutionFeedback>();
        controller.Feedback += received.Add;
        var hello = new ExecutionCommand
        {
            Type = "twin_hello", Version = 1, Protocol = ExecutionController.Protocol,
            RunId = Guid.NewGuid(), ItemId = Guid.NewGuid()
        };
        controller.Receive(hello);
        Check(received.Select(item => item.Status).SequenceEqual(new[] { "accepted", "completed" }), "协议握手");
        var command = new ExecutionCommand
        {
            Type = "product_recognized", Version = 2, RunId = Guid.NewGuid(), Sequence = 1,
            ItemId = Guid.NewGuid(), ProductId = product, ProductType = "测试件"
        };
        received.Clear();
        controller.Receive(command);
        Check(controller.HasPending && simulation.Received == 1 && received.Single().Status == "accepted",
            "接收不冒充执行完成");
        controller.Receive(command);
        Check(simulation.Received == 1, "重复报文不上第二件");
        controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 2 });
        Check(received.Last().Status == "busy" && simulation.Received == 1, "串行执行忙碌反馈");
        for (int index = 0; index < 500; index++)
        {
            simulation.Step();
            controller.CheckHealth();
        }

        Check(simulation.Sorted == 1 && !controller.HasPending
            && received.Any(item => item.Status == "executing")
            && received.Last().Status == "completed", "实际仿真入仓后才回 completed");
        controller.Receive(command);
        Check(simulation.Received == 1 && received.Last().Status == "completed", "已完成报文幂等回放");
        controller.Receive(command with { ItemId = Guid.NewGuid(), ProductId = Guid.NewGuid() });
        Check(received.Last().Status == "failed", "未映射产品不入队");

        controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 3 });
        controller.Abort("操作员复位");
        Check(!controller.HasPending && received.Last().Status == "failed" && simulation.InFlight == 1,
            "取消保留仿真物件并返回失败");
        simulation.Reset(true);
        controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 4 });
        simulation.Control(TwinControl.EmergencyStop, 1);
        simulation.Step();
        controller.CheckHealth();
        Check(received.Last().Status == "failed", "急停联锁反馈");

        simulation.Reset(true);
        simulation.Control(TwinControl.BinCapacity, 3);
        simulation.Step();
        for (int item = 0; item < 3; item++)
        {
            controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = item + 10 });
            for (int tick = 0; tick < 500; tick++)
            {
                simulation.Step();
                controller.CheckHealth();
            }
        }

        controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 20 });
        Check(simulation.Sorted == 3 && received.Last().Status == "failed", "满仓拒绝新执行");
        simulation.Reset(true);
        controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 30 });
        controller.Receive(hello with { Type = "twin_stop", ItemId = Guid.NewGuid() });
        Check(!controller.HasPending && simulation.InFlight == 1
            && received.Any(item => item.Sequence == 30 && item.Status == "failed"), "停止握手取消未决仿真");
        string routesPath = Path.Combine(Path.GetTempPath(), "twin-routes-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var route = new LinkRoute(product, TwinProductKind.Block);
            LinkRouteStore.Save(routesPath, [route]);
            Check(LinkRouteStore.Load(routesPath).Single() == route, "产品 GUID 与料仓映射持久化");
            try
            {
                LinkRouteStore.Save(routesPath, [route, route]);
                Check(false, "重复料仓应拒绝");
            }
            catch (InvalidDataException)
            {
                Check(LinkRouteStore.Load(routesPath).Length == 1, "无效映射不覆盖旧配置");
            }
        }
        finally
        {
            File.Delete(routesPath);
        }

        controller.Disconnected();
        try
        {
            controller.Receive(command with { ItemId = Guid.NewGuid() });
            Check(false, "未握手应拒绝");
        }
        catch (InvalidDataException)
        {
            Check(true, "断开后必须重新握手");
        }

        controller.Receive(hello);
        for (int index = 0; index < 270; index++)
        {
            simulation.Reset(true);
            controller.Receive(command with { ItemId = Guid.NewGuid(), Sequence = 100 + index });
            controller.Abort("缓存测试");
        }

        simulation.Reset(true);
        controller.Receive(command);
        Check(simulation.Received == 0 && received.Last().Status == "failed",
            "反馈缓存淘汰后仍拒绝同运行旧序号重放");

        return checks;
    }

    /// <summary>跨进程测试宿主：使用真实状态机及管道，固定步加速推进，完全不接触实体设备。</summary>
    public static async Task HostAsync(string pipeName)
    {
        object gate = new();
        bool paused = false;
        var simulation = new SortingSimulation((kind, _, _) =>
            new TwinInspection(kind, "测试", true, null, null, null, "测试"));
        simulation.Reset(true);
        var controller = new ExecutionController(simulation, _ => TwinProductKind.Disc, encoded =>
        {
            if (encoded is null)
            {
                return null;
            }

            using var stream = new MemoryStream(Convert.FromBase64String(encoded));
            var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            image.Freeze();
            return image;
        });
        await using var server = new ExecutionServer(pipeName);
        controller.Feedback += server.Publish;
        server.Received = command =>
        {
            lock (gate)
            {
                controller.Receive(command);
                if (command.Type == "twin_stop")
                {
                    paused = true;
                }
                else if (command.Type == "product_recognized" && controller.HasPending)
                {
                    paused = false;
                }
            }

            return Task.CompletedTask;
        };
        server.Disconnected = () =>
        {
            lock (gate)
            {
                controller.Disconnected();
                paused = true;
            }

            return Task.CompletedTask;
        };
        server.State = () =>
        {
            lock (gate)
            {
                return Task.FromResult<object>(new
                {
                    Type = "twin_state", Protocol = ExecutionController.Protocol,
                    SimulationOnly = true, LiveMode = true, Paused = paused,
                    simulation.InFlight, simulation.Sorted, Bins = simulation.Bins.ToArray(),
                    PreviewAvailable = simulation.LastInspection?.Image is not null,
                    simulation.RobotProgress, Time = DateTimeOffset.UtcNow
                });
            }
        };
        server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(timeout.Token))
            {
                lock (gate)
                {
                    if (!paused)
                    {
                        simulation.Step();
                        controller.CheckHealth();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
