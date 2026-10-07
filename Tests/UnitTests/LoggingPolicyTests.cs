using System.Text.Json;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

public sealed class LoggingPolicyTests
{
    [Fact]
    public void StructuredSinkUsesOwnedManifestAndLeavesLegacyFilesUntouched()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var legacyPath = Path.Combine(root.FullName, "mcp-server.log");
        var legacyContent = "legacy historical log\n";
        File.WriteAllText(legacyPath, legacyContent);

        try
        {
            using (var logger = new StructuredFileLogger(
                       root.FullName,
                       new StructuredLogFilePolicy { FilePrefix = "health-" }))
            {
                logger.Info("request completed", category: "health", requestId: "request-1");
                Assert.Equal(StructuredLogHealthStatus.Healthy, logger.Health.Status);
            }

            var manifestPath = Path.Combine(root.FullName, "structured-log.manifest.json");
            var manifestJson = File.ReadAllText(manifestPath);
            Assert.Contains(StructuredLogFilePolicy.ManifestSchema, manifestJson, StringComparison.Ordinal);
            Assert.Contains(StructuredLogFilePolicy.Owner, manifestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(root.FullName, manifestJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(legacyContent, File.ReadAllText(legacyPath));

            var logFiles = Directory.GetFiles(root.FullName, "health-*.jsonl");
            Assert.Single(logFiles);
            Assert.Single(File.ReadAllLines(logFiles[0]));
            Assert.DoesNotContain("request completed", File.ReadAllText(logFiles[0]), StringComparison.Ordinal);
            Assert.Equal(1, loggerHealthFileCount(root.FullName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ILoggerStructuredContextReachesTheAllowlistedRecordFields()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        try
        {
            ILogger logger = new StructuredFileLogger(
                root.FullName,
                new StructuredLogFilePolicy { FilePrefix = "context-" });
            logger.Log(
                LogLevel.Information,
                "request completed",
                new LogContext
                {
                    RequestId = "req-1",
                    CorrelationId = "corr-1",
                    Component = "server",
                    Operation = "health",
                    ErrorCode = "OK",
                    Outcome = "SUCCESS",
                    DurationMs = 7,
                    MessageCode = "REQUEST_COMPLETED"
                });

            var line = File.ReadAllLines(Directory.GetFiles(root.FullName, "context-*.jsonl").Single()).Single();
            using var document = JsonDocument.Parse(line);
            Assert.Equal("corr-1", document.RootElement.GetProperty("correlationId").GetString());
            Assert.Equal("req-1", document.RootElement.GetProperty("requestId").GetString());
            Assert.Equal("health", document.RootElement.GetProperty("operation").GetString());
            Assert.Equal(7, document.RootElement.GetProperty("durationMs").GetInt64());
            (logger as IDisposable)?.Dispose();
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void SizeRotationAndRetentionOnlyManageManifestFiles()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var unknownPath = Path.Combine(root.FullName, "health-99999999.jsonl");
        File.WriteAllText(unknownPath, "unknown file\n");

        try
        {
            var policy = new StructuredLogFilePolicy
            {
                FilePrefix = "health-",
                MaxFileBytes = 256,
                MaxFileAge = TimeSpan.FromHours(1),
                MaxRetainedFiles = 2,
                MaxRetainedBytes = 4096
            };

            using var logger = new StructuredFileLogger(root.FullName, policy);
            for (var index = 0; index < 10; index++)
            {
                logger.Log(new LogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = LogLevel.Information,
                    Category = "test",
                    Message = "rotation record " + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, policy.ManifestFileName)));
            var managedFiles = manifest.RootElement.GetProperty("ManagedFiles").EnumerateArray().Select(item => item.GetString()!).ToArray();
            Assert.InRange(managedFiles.Length, 1, 2);
            Assert.True(File.Exists(unknownPath));
            Assert.DoesNotContain(unknownPath, managedFiles.Select(file => Path.Combine(root.FullName, file)));
            Assert.Equal(StructuredLogHealthStatus.Healthy, logger.Health.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void TimeRotationCreatesASecondOwnedFileDeterministically()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var now = DateTimeOffset.UtcNow;

        try
        {
            var policy = new StructuredLogFilePolicy
            {
                FilePrefix = "time-",
                MaxFileBytes = 4096,
                MaxFileAge = TimeSpan.FromMinutes(1),
                MaxRetainedFiles = 4,
                MaxRetainedBytes = 16384
            };
            using var logger = new StructuredFileLogger(root.FullName, policy, clock: () => now);
            logger.Info("first record");
            now = now.AddMinutes(2);
            logger.Info("second record");

            Assert.Equal(2, Directory.GetFiles(root.FullName, "time-*.jsonl").Length);
            Assert.Equal(2, logger.Health.ManagedFileCount);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ContinuousWritesUseImmutableCreatedTimeAndDoNotRotateEveryAppend()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var now = DateTimeOffset.UtcNow;

        try
        {
            var policy = new StructuredLogFilePolicy
            {
                FilePrefix = "continuous-",
                MaxFileBytes = 4096,
                MaxFileAge = TimeSpan.FromMinutes(1),
                MaxRetainedFiles = 4,
                MaxRetainedBytes = 16384
            };
            using var logger = new StructuredFileLogger(root.FullName, policy, clock: () => now);
            logger.Log(new LogEntry { Timestamp = now, Level = LogLevel.Information, MessageCode = "REQUEST_COMPLETED" });
            now = now.AddSeconds(30);
            logger.Log(new LogEntry { Timestamp = now, Level = LogLevel.Information, MessageCode = "REQUEST_COMPLETED" });

            Assert.Single(Directory.GetFiles(root.FullName, "continuous-*.jsonl"));
            Assert.Equal(2, File.ReadAllLines(Directory.GetFiles(root.FullName, "continuous-*.jsonl").Single()).Length);

            now = now.AddSeconds(31);
            logger.Log(new LogEntry { Timestamp = now, Level = LogLevel.Information, MessageCode = "REQUEST_COMPLETED" });

            var files = Directory.GetFiles(root.FullName, "continuous-*.jsonl");
            Assert.Equal(2, files.Length);
            Assert.Contains(files, file => File.ReadAllLines(file).Length == 2);
            Assert.Contains(files, file => File.ReadAllLines(file).Length == 1);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void TwoInstancesReloadManifestAndDoNotOrphanRotatedFiles()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        try
        {
            var policy = new StructuredLogFilePolicy
            {
                FilePrefix = "multi-",
                MaxFileBytes = 256,
                MaxFileAge = TimeSpan.FromHours(1),
                MaxRetainedFiles = 3,
                MaxRetainedBytes = 4096
            };
            using var first = new StructuredFileLogger(root.FullName, policy);
            using var second = new StructuredFileLogger(root.FullName, policy);
            for (var index = 0; index < 12; index++)
            {
                var logger = index % 2 == 0 ? first : second;
                logger.Log(new LogEntry
                {
                    Level = LogLevel.Information,
                    MessageCode = "REQUEST_COMPLETED",
                    CorrelationId = "corr-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, policy.ManifestFileName)));
            var managedFiles = manifest.RootElement.GetProperty("ManagedFiles")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            var actualFiles = Directory.GetFiles(root.FullName, "multi-*.jsonl")
                .Select(file => Path.GetFileName(file)!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(actualFiles);
            Assert.True(actualFiles.SetEquals(managedFiles));
            Assert.InRange(actualFiles.Count, 1, 3);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ConcurrentWritesProduceCompleteJsonLines()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        try
        {
            using var logger = new StructuredFileLogger(
                root.FullName,
                new StructuredLogFilePolicy { FilePrefix = "concurrent-", MaxFileBytes = 1024 * 1024 });

            Parallel.For(0, 64, index => logger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogLevel.Information,
                Category = "concurrency",
                CorrelationId = "corr-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Message = "worker completed"
            }));

            var lines = Directory.GetFiles(root.FullName, "concurrent-*.jsonl")
                .SelectMany(File.ReadAllLines)
                .ToArray();
            Assert.Equal(64, lines.Length);
            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                Assert.Equal("arcgis-pro-mcp-log-record-v1", document.RootElement.GetProperty("schema").GetString());
            }

            Assert.Equal(StructuredLogHealthStatus.Healthy, logger.Health.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void SinkFailureDegradesAndDoesNotThrowIntoBusinessCaller()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        using var logger = new StructuredFileLogger(root.FullName, new StructuredLogFilePolicy { FilePrefix = "failure-" });
        root.Delete(true);

        var exception = Record.Exception(() => logger.Info("after owned root removal"));

        Assert.Null(exception);
        Assert.Equal(StructuredLogHealthStatus.Degraded, logger.Health.Status);
        Assert.Equal("LOG_WRITE_FAILED", logger.Health.LastFailureCode);
        Assert.Equal(1, logger.Health.DroppedCount);
    }

    [Fact]
    public void ConstructionIofailureDegradesWithoutThrowingBusinessCaller()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var placeholder = Path.Combine(root.FullName, "not-a-directory");
        File.WriteAllText(placeholder, "placeholder");

        try
        {
            using var logger = new StructuredFileLogger(
                placeholder,
                new StructuredLogFilePolicy { FilePrefix = "init-failure-" });

            Assert.Equal(StructuredLogHealthStatus.Degraded, logger.Health.Status);
            Assert.Equal("LOG_INIT_FAILED", logger.Health.LastFailureCode);
            var exception = Record.Exception(() => logger.Info("must not escape"));
            Assert.Null(exception);
            Assert.Equal("LOG_INIT_FAILED", logger.Health.LastFailureCode);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void LockedActiveFileDegradesWithoutThrowingBusinessCaller()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        try
        {
            using var logger = new StructuredFileLogger(
                root.FullName,
                new StructuredLogFilePolicy { FilePrefix = "lock-" });
            logger.Log(new LogEntry { Level = LogLevel.Information, MessageCode = "REQUEST_COMPLETED" });

            var activePath = Directory.GetFiles(root.FullName, "lock-*.jsonl").Single();
            using var lockedFile = new FileStream(activePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var exception = Record.Exception(() => logger.Log(
                new LogEntry { Level = LogLevel.Information, MessageCode = "REQUEST_COMPLETED" }));

            Assert.Null(exception);
            Assert.Equal(StructuredLogHealthStatus.Degraded, logger.Health.Status);
            Assert.Equal("LOG_WRITE_FAILED", logger.Health.LastFailureCode);
            Assert.Equal(1, logger.Health.DroppedCount);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void InvalidManifestFailsClosedWithoutDeletingUnknownContent()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-structured-log-");
        var unknownPath = Path.Combine(root.FullName, "health-00000000.jsonl");
        File.WriteAllText(unknownPath, "unknown file\n");
        var manifestPath = Path.Combine(root.FullName, "structured-log.manifest.json");
        File.WriteAllText(manifestPath, "{\"Schema\":\"wrong\",\"Owner\":\"other\",\"FilePrefix\":\"health-\",\"ManagedFiles\":[]}");

        try
        {
            Assert.Throws<InvalidDataException>(() => new StructuredFileLogger(
                root.FullName,
                new StructuredLogFilePolicy { FilePrefix = "health-" }));
            Assert.True(File.Exists(unknownPath));
            Assert.Equal("unknown file\n", File.ReadAllText(unknownPath));
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static int loggerHealthFileCount(string root)
        => Directory.GetFiles(root, "health-*.jsonl").Length;
}
