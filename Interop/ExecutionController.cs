using System.IO;
using System.Windows.Media.Imaging;

namespace RobotVisionSortingTwin
{
    public sealed record ExecutionCommand
    {
        public string Type { get; init; } = "";
        public string Protocol { get; init; } = "";
        public int Version { get; init; }
        public Guid RunId { get; init; }
        public long Sequence { get; init; }
        public Guid? ItemId { get; init; }
        public Guid ProductId { get; init; }
        public string ProductType { get; init; } = "";
        public double? AreaPx2 { get; init; }
        public double? PerimeterPx { get; init; }
        public TelemetryProduct[] Products { get; init; } = [];
        public string? ImageJpeg { get; init; }
    }

    public sealed record ExecutionFeedback(Guid RunId, long Sequence, Guid ItemId,
        string Status, string Message = "")
    {
        public string Type => "robot_feedback";
        public int Version => 2;
        public bool SimulationOnly => true;
    }

    /// <summary>仅由仿真线程调用；反馈来自实际仿真物件状态，不以网络写入或定时器冒充完成。</summary>
    public sealed class ExecutionController
    {
        public const string Protocol = "robot-vision-twin/execute-v1";
        private readonly SortingSimulation _simulation;
        private readonly Func<Guid, TwinProductKind?> _route;
        private readonly Dictionary<(Guid, Guid), ExecutionFeedback> _recent = [];
        private readonly Queue<(Guid, Guid)> _recentOrder = [];
        private readonly Dictionary<(Guid, Guid), Guid> _knownProducts = [];
        private readonly Dictionary<Guid, long> _runSequences = [];
        private ExecutionCommand? _pending;
        private int _simulationId;
        private bool _welcomed;
        private readonly Func<string?, BitmapSource?>? _decodeImage;
        public event Action<ExecutionFeedback>? Feedback;
        public bool HasPending => _pending is not null;

        public ExecutionController(SortingSimulation simulation, Func<Guid, TwinProductKind?> route,
            Func<string?, BitmapSource?>? decodeImage = null)
        {
            _simulation = simulation;
            _route = route;
            _decodeImage = decodeImage;
            simulation.ExternalItemStateChanged += OnItemState;
        }

        public void Receive(ExecutionCommand command)
        {
            if (command.Type == "twin_heartbeat" && _welcomed)
            {
                return;
            }

            if (command.RunId == Guid.Empty || command.ItemId is not Guid itemId || itemId == Guid.Empty)
            {
                throw new InvalidDataException("执行消息缺少运行或件编号。");
            }

            if (command.Type is "twin_hello" or "twin_session" or "twin_stop")
            {
                if (command.Version != 1 || command.Protocol != Protocol || command.Sequence != 0
                    || command.Type != "twin_hello" && !_welcomed)
                {
                    throw new InvalidDataException("沙盘执行协议握手无效。");
                }

                _welcomed = true;
                if (command.Type == "twin_stop")
                {
                    Abort("平台已停止识别，当前执行取消");
                }

                Reply(command, "accepted");
                Reply(command, "completed", "本机仿真协议已就绪");
                return;
            }

            if (!_welcomed || command.Type != "product_recognized" || command.Version != 2
                || command.Sequence < 1 || command.ProductId == Guid.Empty
                || string.IsNullOrWhiteSpace(command.ProductType) || command.ProductType.Length > 80)
            {
                throw new InvalidDataException("执行消息版本或产品身份无效。");
            }

            var key = (command.RunId, itemId);
            if (_recent.TryGetValue(key, out var known))
            {
                if (known.Sequence != command.Sequence || _knownProducts[key] != command.ProductId)
                {
                    Reply(command, "failed", "同一件编号不能对应不同序号或产品");
                    return;
                }

                // 重复消息只回放状态，不创建第二个仿真物件。
                if (known.Status is "executing" or "completed")
                {
                    Reply(command, "accepted");
                }

                Feedback?.Invoke(known);
                return;
            }

            if ((_runSequences.TryGetValue(command.RunId, out long latest) && command.Sequence <= latest)
                || (!_runSequences.ContainsKey(command.RunId) && _runSequences.Count >= 1024))
            {
                Reply(command, "failed", "运行序号已处理或会话容量已满，拒绝重放执行");
                return;
            }

            if (!_simulation.LiveMode)
            {
                Reply(command, "failed", "请在沙盘勾选跟随平台并设置产品料仓");
                return;
            }

            var route = _route(command.ProductId);
            if (route is null || route == TwinProductKind.Reject)
            {
                Reply(command, "failed", "产品尚未映射到 A/B/C 料仓");
                return;
            }

            if (_pending is not null || _simulation.InFlight > 0)
            {
                Reply(command, "busy", "沙盘仍有在途物件，请完成或复位后再发送");
                return;
            }

            if (_simulation.EmergencyStopped || _simulation.Bins[(int)route.Value] >= _simulation.BinCapacity)
            {
                Reply(command, "failed", "仿真急停或目标料仓已满");
                return;
            }

            var inspection = new TwinInspection(route.Value, command.ProductType, true,
                command.AreaPx2, command.PerimeterPx, _decodeImage?.Invoke(command.ImageJpeg),
                $"双向联动：{command.ProductType} → 料仓 {(char)('A' + (int)route.Value)}");
            if (!_simulation.ReceiveExternal(inspection, out _simulationId))
            {
                Reply(command, "busy", "仿真队列不可用");
                return;
            }

            _pending = command;
            _runSequences[command.RunId] = command.Sequence;
            Remember(command, "accepted");
        }

        private void OnItemState(int id, string state)
        {
            if (_pending is null || id != _simulationId)
            {
                return;
            }

            var command = _pending;
            Remember(command, state);
            if (state == "completed")
            {
                _pending = null;
            }
        }

        public void CheckHealth()
        {
            if (_pending is not null && (_simulation.EmergencyStopped || _simulation.Alarm.Length > 0))
            {
                Abort("仿真联锁：" + _simulation.Alarm);
            }
        }

        public void Abort(string reason)
        {
            if (_pending is not null)
            {
                var command = _pending;
                _pending = null;
                Remember(command, "failed", reason);
            }
        }

        public void Disconnected()
        {
            Abort("平台连接中断，仿真物件保留，等待现场复位");
            _welcomed = false;
        }

        private void Remember(ExecutionCommand command, string state, string message = "")
        {
            var key = (command.RunId, command.ItemId!.Value);
            var feedback = new ExecutionFeedback(command.RunId, command.Sequence, command.ItemId!.Value, state, message);
            if (!_recent.ContainsKey(key))
            {
                _recentOrder.Enqueue(key);
                _knownProducts[key] = command.ProductId;
                if (_recentOrder.Count > 256)
                {
                    var expired = _recentOrder.Dequeue();
                    _recent.Remove(expired);
                    _knownProducts.Remove(expired);
                }
            }

            _recent[key] = feedback;
            Feedback?.Invoke(feedback);
        }

        private void Reply(ExecutionCommand command, string state, string message = "")
        {
            Feedback?.Invoke(new ExecutionFeedback(command.RunId, command.Sequence,
                command.ItemId!.Value, state, message));
        }
    }
}
