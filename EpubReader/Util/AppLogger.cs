using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EpubReader.Util;

public static class AppLogger
{
	static ILoggerFactory? loggerFactory;

	public static void Initialize(ILoggerFactory factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		loggerFactory = factory;
	}

	public static ILogger CreateLogger<T>()
	{
		return ResolveFactory().CreateLogger<T>();
	}

	public static ILogger CreateLogger(string categoryName)
	{
		if (string.IsNullOrWhiteSpace(categoryName))
		{
			throw new ArgumentException("Logger category name cannot be null or empty.", nameof(categoryName));
		}

		return ResolveFactory().CreateLogger(categoryName);
	}

	static ILoggerFactory ResolveFactory()
	{
		if (loggerFactory is not null)
		{
			return loggerFactory;
		}

		var services = Application.Current?.Handler?.MauiContext?.Services;
		var resolvedFactory = services?.GetService<ILoggerFactory>();
		if (resolvedFactory is not null)
		{
			loggerFactory = resolvedFactory;
			return resolvedFactory;
		}

		return NullLoggerFactory.Instance;
	}
}

public static class LoggerCompatibilityExtensions
{
	public static void Info(this ILogger logger, string message)
	{
		ArgumentNullException.ThrowIfNull(logger);
		logger.LogInformation("{Message}", message);
	}

	public static void Warn(this ILogger logger, string message)
	{
		ArgumentNullException.ThrowIfNull(logger);
		logger.LogWarning("{Message}", message);
	}

	public static void Error(this ILogger logger, string message)
	{
		ArgumentNullException.ThrowIfNull(logger);
		logger.LogError("{Message}", message);
	}
}

public sealed partial class TraceLoggerProvider : ILoggerProvider
{
	public ILogger CreateLogger(string categoryName)
	{
		return new TraceLogger(categoryName);
	}

	public void Dispose()
	{
	}

	sealed class TraceLogger(string categoryName) : ILogger
	{
		readonly string categoryName = categoryName;

		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel != LogLevel.None;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter);

			if (!IsEnabled(logLevel))
			{
				return;
			}

			var message = formatter(state, exception);
			if (string.IsNullOrWhiteSpace(message) && exception is null)
			{
				return;
			}

			Trace.TraceInformation($"{DateTimeOffset.Now:O} [{logLevel}] {categoryName}: {message}");
			if (exception is not null)
			{
				Trace.TraceError(exception.ToString());
			}
		}
	}

	sealed partial class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new();

		public void Dispose()
		{
		}
	}
}