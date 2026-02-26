using Serilog;
using Serilog.Events;

namespace HearthstoneServer;

/// <summary>
/// 全局日志配置。在应用启动时调用一次 Init()。
/// </summary>
public static class LogConfig
{
	private static bool _initialized;

	/// <summary>
	/// 初始化 Serilog 全局日志。
	/// </summary>
	/// <param name="logFile">日志文件路径（null = 不写文件）</param>
	/// <param name="minimumLevel">最低日志级别，默认 Debug</param>
	/// <param name="enableConsole">是否输出到控制台，默认 true</param>
	public static void Init(
		string? logFile = null,
		LogEventLevel minimumLevel = LogEventLevel.Debug,
		bool enableConsole = true)
	{
		if (_initialized) return;

		var config = new LoggerConfiguration()
			.MinimumLevel.Is(minimumLevel)
			.Enrich.FromLogContext();

		if (enableConsole)
			config.WriteTo.Console(
				outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

		if (logFile != null)
			config.WriteTo.File(
				logFile,
				outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
				rollingInterval: RollingInterval.Day);

		Log.Logger = config.CreateLogger();
		_initialized = true;
	}

	/// <summary>
	/// 为指定类型创建上下文化的 ILogger（SourceContext = 类型全名）。
	/// </summary>
	public static ILogger ForType<T>() => Log.ForContext<T>();

	/// <summary>
	/// 为指定类型名创建上下文化的 ILogger。
	/// </summary>
	public static ILogger ForType(Type type) => Log.ForContext(type);

	/// <summary>
	/// 关闭日志（确保文件缓冲刷写）。
	/// </summary>
	public static void Close()
	{
		Log.CloseAndFlush();
		_initialized = false;
	}
}
