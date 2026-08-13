using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>
/// 本地 stdio MCP 进程客户端实现：以 JSON-RPC 2.0（UTF-8 换行分隔帧）与本地 MCP Server
/// 子进程（如 xhs-mcp、jianying-mcp）通信。懒启动：首次工具调用时启动进程并完成
/// initialize 握手；进程意外退出后下一次调用自动重建。单次工具调用串行执行（信号量），
/// 请求-响应按自增 id 关联；响应行解析失败仅跳过，不中断后续读取。
/// </summary>
public sealed class StdioMcpProcessClient : IMcpProcessClient
{
    private const string ProtocolVersion = "2025-03-26";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly McpProcessOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _processLock = new();
    private Process? _process;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private StreamReader? _stderrReader;
    private int _nextId;

    /// <summary>构造本地 stdio MCP 进程客户端。</summary>
    /// <param name="options">进程启动命令与超时配置。</param>
    public StdioMcpProcessClient(McpProcessOptions options) => _options = options;

    /// <inheritdoc />
    public async Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolName)) throw new McpClientException("MCP 工具名称不能为空。");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var result = await SendAsync("tools/call",
                new JsonObject { ["name"] = toolName, ["arguments"] = arguments ?? new JsonObject() }, timeout.Token);
            if (result is null) return null;
            if (result["isError"]?.GetValue<bool>() == true)
                throw new McpClientException(ReadContentText(result) ?? "MCP 工具执行失败。");
            return JsonNode.Parse(ReadContentText(result) ?? "null");
        }
        catch
        {
            // 超时/取消/异常后进程状态不可信（stdout 可能残留未完成读操作），终止并重建进程；
            // 部署校准 2026-08-09：WaitAsync 取消不会取消底层 StreamReader.ReadLineAsync，流被占用导致后续调用冲突
            lock (_processLock)
            {
                TryKillProcess();
            }
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<JsonObject?> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            return await SendAsync("tools/list", new JsonObject(), timeout.Token) as JsonObject;
        }
        catch
        {
            lock (_processLock)
            {
                TryKillProcess();
            }
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_processLock)
        {
            TryKillProcess();
        }
        return Task.CompletedTask;
    }

    /// <summary>懒启动 MCP 进程并完成 initialize 握手；进程已存活或已握手时直接返回。</summary>
    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        lock (_processLock)
        {
            if (_process is { HasExited: false }) return;
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.CommandFileName,
                Arguments = _options.Arguments,
                WorkingDirectory = _options.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // 部署校准 2026-08-09：输入流必须无 BOM——Encoding.UTF8 默认带 EF BB BF，node MCP Server 收到后 JSON 解析失败、永不响应
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            try
            {
                _process = Process.Start(startInfo) ?? throw new McpClientException("本地 MCP 进程启动失败。");
            }
            catch (Exception error) when (error is not McpClientException)
            {
                throw new McpClientException($"本地 MCP 进程无法启动：{error.Message}", error);
            }
            _writer = _process.StandardInput;
            _reader = _process.StandardOutput;
            _stderrReader = _process.StandardError;
            // 部署校准 2026-08-09：stderr 管道必须持续排空，否则缓冲写满会导致 MCP 进程（如 xhs-mcp 的 Puppeteer 日志）阻塞死锁
            _ = Task.Run(() => DrainStderrAsync());
        }
        await SendAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "nexusmind-backend", ["version"] = "1.0.0" }
        }, cancellationToken);
        // 部署校准 2026-08-10：MCP 协议要求 initialize 成功后发送 notifications/initialized 通知；
        // FastMCP（python SDK）收到前 tools/list 返回空、tools/call 报 -32602（xhs-mcp 的 node SDK 不强制，此前未暴露）
        await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
    }

    /// <summary>发送一条 JSON-RPC 通知（无 id、不等待响应）；用于握手后通知服务器初始化完成。</summary>
    private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
    {
        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        };
        await _writer!.WriteLineAsync(notification.ToJsonString(JsonOptions)).WaitAsync(cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    /// <summary>发送一条 JSON-RPC 请求并读取匹配 id 的响应；工具结果返回 result 节点，进程退出或响应超时抛异常。</summary>
    private async Task<JsonNode?> SendAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        };
        await _writer!.WriteLineAsync(request.ToJsonString(JsonOptions)).WaitAsync(cancellationToken);
        await _writer.FlushAsync(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await _reader!.ReadLineAsync().WaitAsync(cancellationToken);
            if (line is null) throw new McpClientException("本地 MCP 进程意外退出。");
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }
            if (node is not JsonObject response || response["id"]?.GetValue<int>() != id) continue;
            if (response["error"] is JsonObject error)
                throw new McpClientException(error["message"]?.GetValue<string>() ?? "MCP 调用失败。");
            return response["result"] as JsonObject;
        }
    }

    /// <summary>读取工具结果 content 数组首个 text 条目的文本；无文本条目返回 null。</summary>
    private static string? ReadContentText(JsonNode result)
    {
        if (result["content"] is not JsonArray content) return null;
        foreach (var entry in content)
        {
            if (entry is JsonObject item && item["type"]?.GetValue<string>() == "text" && item["text"]?.GetValue<string>() is { } text)
                return text;
        }
        return null;
    }

    /// <summary>持续读取并丢弃 MCP 进程 stderr，防止管道缓冲写满阻塞进程。</summary>
    private async Task DrainStderrAsync()
    {
        try
        {
            while (_stderrReader is not null && await _stderrReader.ReadLineAsync() is not null) { }
        }
        catch (ObjectDisposedException)
        {
            // 进程已清理，读取结束。
        }
        catch (IOException)
        {
            // 进程退出导致的管道中断，无需处理。
        }
    }

    /// <summary>终止并清理 MCP 进程及其管道；幂等。</summary>
    private void TryKillProcess()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 进程已退出或不可达，无需处理。
            }
            _process.Dispose();
        }
        _process = null;
        _writer = null;
        _reader = null;
        _stderrReader = null;
    }
}
