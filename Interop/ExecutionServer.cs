using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace RobotVisionSortingTwin
{
    /// <summary>当前用户的独立双向管道；命令接收、反馈发送和心跳互不阻塞界面线程。</summary>
    public sealed class ExecutionServer : IAsyncDisposable
    {
        public const string PipeName = "RobotVisionSortingTwin.Execution.v1";
        private sealed class Connection(NamedPipeServerStream pipe, CancellationToken token)
        {
            public NamedPipeServerStream Pipe { get; } = pipe;
            public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
            public Channel<string> Queue { get; } = Channel.CreateBounded<string>(64);
            public long LastRead = DateTime.UtcNow.Ticks;
        }

        private readonly string _pipeName;
        private readonly CancellationTokenSource _stop = new();
        private Connection? _connection;
        private Task? _worker;
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            MaxDepth = 24
        };

        public Func<ExecutionCommand, Task>? Received { get; set; }
        public Func<Task<object>>? State { get; set; }
        public Func<Task>? Disconnected { get; set; }
        public bool IsConnected => Volatile.Read(ref _connection) is not null;

        public ExecutionServer(string pipeName = PipeName)
        {
            _pipeName = pipeName;
        }

        public void Start()
        {
            _worker ??= Task.Run(ListenAsync);
        }

        public void Publish(ExecutionFeedback feedback)
        {
            Enqueue(Volatile.Read(ref _connection), feedback);
        }

        private static void Enqueue(Connection? connection, object message)
        {
            if (connection is not null && !connection.Queue.Writer.TryWrite(JsonSerializer.Serialize(message, Json)))
            {
                connection.Stop.Cancel();
            }
        }

        private async Task ListenAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    var connection = new Connection(pipe, _stop.Token);
                    Volatile.Write(ref _connection, connection);
                    var reading = ReadAsync(connection);
                    var writing = WriteAsync(connection);
                    var heartbeat = HeartbeatAsync(connection);
                    try
                    {
                        await Task.WhenAny(reading, writing, heartbeat);
                    }
                    finally
                    {
                        connection.Stop.Cancel();
                        pipe.Dispose();
                        try
                        {
                            await Task.WhenAll(reading, writing, heartbeat);
                        }
                        catch (Exception)
                        {
                            // 状态机在断开回调中保留未决件；不从网络任务伪造完成。
                        }

                        Volatile.Write(ref _connection, null);
                        if (Disconnected is not null)
                        {
                            await Disconnected();
                        }

                        connection.Stop.Dispose();
                    }
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    try
                    {
                        await Task.Delay(500, _stop.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private async Task ReadAsync(Connection connection)
        {
            using var reader = new StreamReader(connection.Pipe, new UTF8Encoding(false, true), false, 4096, true);
            var buffer = new char[4096];
            var line = new StringBuilder();
            while (true)
            {
                int count = await reader.ReadAsync(buffer, connection.Stop.Token);
                if (count == 0)
                {
                    return;
                }

                for (int index = 0; index < count; index++)
                {
                    if (buffer[index] == '\n')
                    {
                        var command = JsonSerializer.Deserialize<ExecutionCommand>(line.ToString(), Json)
                            ?? throw new InvalidDataException("空执行消息。");
                        if (command.ImageJpeg?.Length > 48000 || command.Products is null || command.Products.Length > 128
                            || command.Products.Any(product => product is null || product.Id == Guid.Empty
                                || string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 80)
                            || command.Products.Select(product => product.Id).Distinct().Count() != command.Products.Length)
                        {
                            throw new InvalidDataException("产品目录格式无效。");
                        }

                        Interlocked.Exchange(ref connection.LastRead, DateTime.UtcNow.Ticks);
                        if (Received is not null)
                        {
                            await Received(command);
                        }

                        line.Clear();
                    }
                    else
                    {
                        if (line.Length >= 65536)
                        {
                            throw new InvalidDataException("执行消息超过 64 KiB。");
                        }

                        line.Append(buffer[index]);
                    }
                }
            }
        }

        private static async Task WriteAsync(Connection connection)
        {
            await foreach (string message in connection.Queue.Reader.ReadAllAsync(connection.Stop.Token))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(1));
                await connection.Pipe.WriteAsync(Encoding.UTF8.GetBytes(message + "\n"), timeout.Token);
            }
        }

        private async Task HeartbeatAsync(Connection connection)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(connection.Stop.Token))
            {
                if (DateTime.UtcNow.Ticks - Interlocked.Read(ref connection.LastRead) > TimeSpan.FromSeconds(5).Ticks)
                {
                    throw new IOException("平台执行心跳超时。");
                }

                if (State is not null)
                {
                    Enqueue(connection, await State());
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            if (_worker is not null)
            {
                await _worker;
            }

            _stop.Dispose();
        }
    }
}
