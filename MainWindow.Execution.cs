using System.IO;
using System.Windows;

namespace RobotVisionSortingTwin
{
    public partial class MainWindow
    {
        private readonly ExecutionServer _commands = new();
        private ExecutionController _execution = null!;
        private readonly Guid[] _savedRouteIds = new Guid[3];

        /// <summary>独立执行通道与只读遥测分离，避免一次采样同时生成两件仿真物件。</summary>
        private void InitializeExecution()
        {
            try
            {
                foreach (var route in LinkRouteStore.Load(LinkRouteStore.DefaultPath))
                {
                    _savedRouteIds[(int)route.Bin] = route.ProductId;
                    _routes[route.ProductId] = route.Bin;
                }
            }
            catch (Exception ex)
            {
                LinkDetails.Text = "料仓映射恢复失败，请重新设置：" + ex.Message;
            }

            _execution = new ExecutionController(_simulation,
                id => _routes.TryGetValue(id, out var route) ? route : null, DecodeImage);
            _execution.Feedback += feedback =>
            {
                _commands.Publish(feedback);
                string state = feedback.Status switch
                {
                    "accepted" => "已接收",
                    "executing" => "仿真执行中",
                    "completed" => "仿真已完成",
                    "busy" => "忙碌",
                    _ => "执行异常"
                };
                ExecutionText.Text = feedback.Sequence == 0 ? "双向仿真协议已就绪，等待识别命令。"
                    : $"{state} · {feedback.Message}";
                ExecutionBanner.Text = ExecutionText.Text;
            };
            _commands.Received = async command =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_closing)
                    {
                        throw new IOException("沙盘正在关闭。");
                    }

                    if (command.Type == "twin_session")
                    {
                        UpdateProductChoices(command.Products);
                    }

                    _execution.Receive(command);
                    if (command.Type == "twin_stop")
                    {
                        _playing = false;
                        _sourceRunning = false;
                        ExecutionText.Text = "平台识别已停止，沙盘仿真暂停。";
                        ExecutionBanner.Text = ExecutionText.Text;
                    }
                    ConnectionText.Text = "平台已连接 · 双向仿真执行（仅本机）";
                    if (_execution.HasPending && command.Type == "product_recognized")
                    {
                        _sourceRunning = true;
                        _playing = true;
                        _lastTime = _clock.Elapsed.TotalSeconds;
                    }
                });
            };
            _commands.State = async () => await Dispatcher.InvokeAsync<object>(() => new
            {
                Type = "twin_state",
                Protocol = ExecutionController.Protocol,
                SimulationOnly = true,
                _simulation.LiveMode,
                Paused = !_playing,
                _simulation.EmergencyStopped,
                _simulation.Alarm,
                _simulation.InFlight,
                _simulation.Sorted,
                Bins = _simulation.Bins.ToArray(),
                _simulation.RobotProgress,
                PreviewAvailable = _simulation.LastInspection?.Image is not null,
                Time = DateTimeOffset.UtcNow
            });
            _commands.Disconnected = async () =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _execution.Disconnected();
                    _playing = false;
                    _sourceRunning = false;
                    ExecutionText.Text = "双向执行通道已断开；保留仿真物件，未决结果待复位。";
                    ExecutionBanner.Text = ExecutionText.Text;
                });
            };
        }
    }
}
