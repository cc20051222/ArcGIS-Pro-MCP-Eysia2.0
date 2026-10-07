using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

public sealed class StructuredLoggingDiagnosticExportTests
{
    [Fact]
    public void ProductionManagedLoggerWritesOnlyAuditedStructuredFields()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-production-log-");
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(FindCompatibilityAssemblyPath());
            var loggerType = assembly.GetType(
                "ArcGISProMCP.Compatibility.Logging.ManagedFileLogger",
                throwOnError: true)!;
            var logger = (ILogger)Activator.CreateInstance(
                loggerType,
                new object?[] { root.FullName, null, null, null })!;

            logger.Error(
                "C:\\Users\\Alice\\private.gdb payload",
                category: "C:\\Users\\Alice",
                requestId: "request/with/path",
                exception: new InvalidOperationException("provider=secret token=abc"));
            logger.Log(new LogEntry
            {
                Level = LogLevel.Information,
                Category = "normal",
                Component = "server",
                Operation = "request",
                CorrelationId = "corr-1",
                MessageCode = "REQUEST_COMPLETED",
                Outcome = "SUCCESS",
                ErrorCode = "NONE",
                Message = "GIS result data must not persist",
                Result = "Seattle"
            });

            var json = File.ReadAllText(Directory.GetFiles(root.FullName, "mcp-structured-*.jsonl").Single());
            Assert.DoesNotContain("Alice", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private.gdb", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("provider=secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Seattle", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"message\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"result\"", json, StringComparison.OrdinalIgnoreCase);

            using var document = JsonDocument.Parse(json.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]);
            var record = document.RootElement;
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("component").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("operation").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("correlationId").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("messageCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("errorCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("outcome").GetString()));

            (logger as IDisposable)?.Dispose();
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ProductionNormalizerRejectsUserControlledIdentifiersInManagedJsonAndExport()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-adversarial-log-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-adversarial-export-");
        try
        {
            var normalized = ProductionLogEntryNormalizer.Normalize(new LogEntry
            {
                Level = LogLevel.Information,
                Category = "Alice",
                Component = "Seattle",
                Operation = "Account123",
                RequestId = "Project42",
                CorrelationId = "Alice",
                Client = "Seattle",
                Tool = "Project42",
                ErrorCode = "Account123",
                MessageCode = "Seattle",
                Outcome = "Alice",
                Message = "JSON-RPC request/tool Project42",
                Result = "Seattle",
                Error = "Account123"
            });

            Assert.Equal("production", normalized.Category);
            Assert.Equal("production", normalized.Component);
            Assert.Equal("event", normalized.Operation);
            Assert.Null(normalized.RequestId);
            Assert.Equal("production", normalized.CorrelationId);
            Assert.Null(normalized.Client);
            Assert.Null(normalized.Tool);
            Assert.Equal("NONE", normalized.ErrorCode);
            Assert.Equal("PRODUCTION_EVENT", normalized.MessageCode);
            Assert.Equal("OK", normalized.Outcome);

            using (var logger = new StructuredFileLogger(source.FullName))
            {
                logger.Log(normalized);
            }

            var destination = Path.Combine(parent.FullName, "adversarial-export");
            var result = new DiagnosticExportService(source.FullName).Export(destination, Snapshot());

            Assert.True(result.Succeeded);
            var allText = string.Join(
                "\n",
                Directory.GetFiles(destination).Select(File.ReadAllText));
            foreach (var forbidden in new[] { "Alice", "Seattle", "Account123", "Project42" })
            {
                Assert.DoesNotContain(forbidden, allText, StringComparison.Ordinal);
            }
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Fact]
    public void CompositionUsesOneManagedProductionSinkAndLeavesLegacyLoggerClassUnwired()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Source", "ArcGISProMCP.Compatibility", "Composition.cs"));

        Assert.Equal(1, Count(source, "new ManagedFileLogger(paths.ManagedLogDirectory)"));
        Assert.DoesNotContain("new Logging.FileLogger", source, StringComparison.Ordinal);
        Assert.Contains("public static string ManagedLogDir", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RepoRoot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Program Files\\ArcGIS", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c.Register<IDiagnosticExportService>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfTestSourceDoesNotPersistResultDataOrLegacyArtifacts()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Source", "ArcGISProMCP.Compatibility", "SelfTestRunner.cs"));

        Assert.DoesNotContain("result.Data", source, StringComparison.Ordinal);
        Assert.DoesNotContain("selftest.log", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selftest.marker", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.WriteAll", source, StringComparison.Ordinal);
        Assert.Contains("SELFTEST_TOOL_COMPLETED", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportPublishesExactAllowlistAndVerifiableManifest()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            WriteManagedRecord(source.FullName, "safe-correlation");
            var destination = Path.Combine(parent.FullName, "new-export");
            var result = new DiagnosticExportService(source.FullName).Export(destination, Snapshot());

            Assert.Equal(DiagnosticExportStatus.Success, result.Status);
            Assert.Equal(3, result.ArtifactCount);
            Assert.True(Directory.Exists(destination));
            Assert.Equal(
                new[] { "health-snapshot.json", "managed-log.jsonl", "manifest.json" },
                Directory.GetFiles(destination).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "manifest.json")));
            Assert.Equal(DiagnosticExportService.ExportManifestSchema, manifest.RootElement.GetProperty("schema").GetString());
            Assert.Equal(DiagnosticExportService.ExportPolicyVersion, manifest.RootElement.GetProperty("policyVersion").GetString());
            var artifacts = manifest.RootElement.GetProperty("artifacts").EnumerateArray().ToArray();
            Assert.Equal(2, artifacts.Length);
            foreach (var artifact in artifacts)
            {
                var name = artifact.GetProperty("name").GetString()!;
                var bytes = File.ReadAllBytes(Path.Combine(destination, name));
                Assert.Equal(bytes.LongLength, artifact.GetProperty("byteLength").GetInt64());
                Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), artifact.GetProperty("sha256").GetString());
            }

            var allText = string.Join("\n", Directory.GetFiles(destination).Select(File.ReadAllText));
            Assert.DoesNotContain(source.FullName, allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"message\"", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"result\"", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"error\"", allText, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetDirectories(
                parent.FullName,
                ".arcgis-pro-mcp-diagnostic-*.writing",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Theory]
    [InlineData(DiagnosticExportService.HealthArtifactName)]
    [InlineData(DiagnosticExportService.ManagedLogArtifactName)]
    public void ExportFailureAfterKnownArtifactLeavesNoDestinationOrTemporarySibling(string failAfterArtifact)
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            WriteManagedRecord(source.FullName, "123");
            var destination = Path.Combine(parent.FullName, "failed-export");
            var service = new DiagnosticExportService(
                source.FullName,
                logPolicy: null,
                afterArtifactWritten: (_, artifactName) =>
                {
                    if (artifactName == failAfterArtifact)
                    {
                        throw new InvalidOperationException("forced test failure");
                    }
                });

            var result = service.Export(destination, Snapshot());

            Assert.Equal(DiagnosticExportStatus.Failed, result.Status);
            Assert.Equal("EXPORT_FAILED", result.ErrorCode);
            Assert.False(Directory.Exists(destination));
            Assert.Empty(Directory.GetDirectories(
                parent.FullName,
                ".arcgis-pro-mcp-diagnostic-*.writing",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Fact]
    public void ExportCleanupRefusesUnknownTemporaryEntries()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        string? unknownPath = null;
        try
        {
            var destination = Path.Combine(parent.FullName, "failed-export");
            var service = new DiagnosticExportService(
                source.FullName,
                logPolicy: null,
                afterArtifactWritten: (temporary, artifactName) =>
                {
                    if (artifactName == DiagnosticExportService.HealthArtifactName)
                    {
                        unknownPath = Path.Combine(temporary, "unknown-entry.txt");
                        File.WriteAllText(unknownPath, "must remain until the test-owned root is removed");
                        throw new InvalidOperationException("forced test failure");
                    }
                });

            var result = service.Export(destination, Snapshot());

            Assert.Equal(DiagnosticExportStatus.Failed, result.Status);
            Assert.Equal("EXPORT_FAILED", result.ErrorCode);
            Assert.False(Directory.Exists(destination));
            Assert.NotNull(unknownPath);
            Assert.True(File.Exists(unknownPath));
            Assert.Single(Directory.GetDirectories(
                parent.FullName,
                ".arcgis-pro-mcp-diagnostic-*.writing",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [ReparsePointFact]
    public void ExportCleanupRefusesReparseTemporaryEntries()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        string? reparsePath = null;
        try
        {
            var target = Directory.CreateDirectory(Path.Combine(parent.FullName, "reparse-target"));
            var destination = Path.Combine(parent.FullName, "failed-export");
            var service = new DiagnosticExportService(
                source.FullName,
                logPolicy: null,
                afterArtifactWritten: (temporary, artifactName) =>
                {
                    if (artifactName == DiagnosticExportService.HealthArtifactName)
                    {
                        reparsePath = Path.Combine(temporary, "unknown-link");
                        Assert.True(TryCreateDirectoryLink(reparsePath, target.FullName, out var reason), reason);

                        throw new InvalidOperationException("forced test failure");
                    }
                });

            var result = service.Export(destination, Snapshot());

            Assert.Equal(DiagnosticExportStatus.Failed, result.Status);
            Assert.Equal("EXPORT_FAILED", result.ErrorCode);
            Assert.False(Directory.Exists(destination));
            Assert.NotNull(reparsePath);
            Assert.True(Directory.Exists(reparsePath));
            Assert.True((File.GetAttributes(reparsePath) & FileAttributes.ReparsePoint) != 0);
            Assert.Single(Directory.GetDirectories(
                parent.FullName,
                ".arcgis-pro-mcp-diagnostic-*.writing",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [ReparsePointFact]
    public void ExportRejectsReparseAncestorsForSourceAndDestination()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-reparse-");
        try
        {
            var sourceTarget = Directory.CreateDirectory(Path.Combine(root.FullName, "source-target"));
            var sourceContainer = Directory.CreateDirectory(Path.Combine(root.FullName, "source-container"));
            var sourceLink = Path.Combine(sourceContainer.FullName, "link");
            Assert.True(TryCreateDirectoryLink(sourceLink, sourceTarget.FullName, out var sourceReason), sourceReason);

            var managedTarget = Directory.CreateDirectory(Path.Combine(sourceTarget.FullName, "managed"));
            WriteManagedRecord(managedTarget.FullName, "123");
            var sourceThroughLink = Path.Combine(sourceLink, "managed");
            var normalDestinationParent = Directory.CreateDirectory(Path.Combine(root.FullName, "normal-parent"));
            var sourceResultDestination = Path.Combine(normalDestinationParent.FullName, "source-export");

            var sourceResult = new DiagnosticExportService(sourceThroughLink).Export(
                sourceResultDestination,
                Snapshot());

            Assert.Equal(DiagnosticExportStatus.Degraded, sourceResult.Status);
            Assert.Equal("MANAGED_LOG_UNAVAILABLE", sourceResult.ErrorCode);
            var degradedLog = File.ReadAllText(Path.Combine(sourceResultDestination, "managed-log.jsonl"));
            Assert.DoesNotContain("\"correlationId\":\"123\"", degradedLog, StringComparison.Ordinal);

            var destinationTarget = Directory.CreateDirectory(Path.Combine(root.FullName, "destination-target"));
            var destinationContainer = Directory.CreateDirectory(Path.Combine(root.FullName, "destination-container"));
            var destinationLink = Path.Combine(destinationContainer.FullName, "link");
            Assert.True(TryCreateDirectoryLink(destinationLink, destinationTarget.FullName, out var destinationReason), destinationReason);

            Directory.CreateDirectory(Path.Combine(destinationTarget.FullName, "nested"));
            var destinationThroughLink = Path.Combine(destinationLink, "nested", "destination-export");
            var destinationResult = new DiagnosticExportService(managedTarget.FullName).Export(
                destinationThroughLink,
                Snapshot());

            Assert.Equal(DiagnosticExportStatus.Failed, destinationResult.Status);
            Assert.Equal("DESTINATION_PARENT_UNAVAILABLE", destinationResult.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(destinationTarget.FullName, "nested", "destination-export")));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ExportSanitizesHealthSnapshotAndKeepsOnlyKnownClientFacts()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            var snapshot = Snapshot() with
            {
                ProductIdentity = "C:\\Users\\Alice\\provider-model",
                Configuration = new HealthComponent(
                    "configuration",
                    HealthComponentStatus.Failure,
                    summary: "payload=secret C:\\private",
                    errorCode: "unknown-secret-code"),
                Clients = new[]
                {
                    new ClientHealthFact("codex", HealthComponentStatus.NotChecked, optional: false, summary: "C:\\private"),
                    new ClientHealthFact("unknown-client", HealthComponentStatus.Pass)
                }
            };

            var destination = Path.Combine(parent.FullName, "health-export");
            var result = new DiagnosticExportService(source.FullName).Export(destination, snapshot);
            Assert.True(result.Succeeded);

            var health = File.ReadAllText(Path.Combine(destination, "health-snapshot.json"));
            Assert.DoesNotContain("Alice", health, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("provider", health, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("payload", health, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unknown-secret-code", health, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("codex", health, StringComparison.Ordinal);
            Assert.DoesNotContain("unknown-client", health, StringComparison.Ordinal);
            Assert.Contains("NotTracked", health, StringComparison.Ordinal);
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Fact]
    public void InvalidOwnedManifestProducesFixedDegradedArtifactWithoutRawFallback()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            File.WriteAllText(
                Path.Combine(source.FullName, "structured-log.manifest.json"),
                "{\"Schema\":\"wrong\",\"Owner\":\"other\",\"ManagedFiles\":[\"unknown.jsonl\"]}");
            File.WriteAllText(Path.Combine(source.FullName, "unknown.jsonl"), "C:\\private\\payload Seattle");

            var result = new DiagnosticExportService(source.FullName).Export(
                Path.Combine(parent.FullName, "degraded-export"),
                Snapshot());

            Assert.Equal(DiagnosticExportStatus.Degraded, result.Status);
            var log = File.ReadAllText(Path.Combine(parent.FullName, "degraded-export", "managed-log.jsonl"));
            Assert.Contains("MANAGED_LOG_UNAVAILABLE", log, StringComparison.Ordinal);
            Assert.DoesNotContain("private", log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Seattle", log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unknown.jsonl", log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Fact]
    public void ExportRejectsExistingDestinationAndTraversalWithoutMutation()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            var existing = Directory.CreateDirectory(Path.Combine(parent.FullName, "existing"));
            var sentinel = Path.Combine(existing.FullName, "sentinel.txt");
            File.WriteAllText(sentinel, "keep");
            var service = new DiagnosticExportService(source.FullName);

            var existingResult = service.Export(existing.FullName, Snapshot());
            Assert.Equal(DiagnosticExportStatus.Failed, existingResult.Status);
            Assert.Equal("DESTINATION_EXISTS", existingResult.ErrorCode);
            Assert.Equal("keep", File.ReadAllText(sentinel));

            var traversalResult = service.Export(
                Path.Combine(parent.FullName, "..", "escaped-export"),
                Snapshot());
            Assert.Equal(DiagnosticExportStatus.Failed, traversalResult.Status);
            Assert.Equal("DESTINATION_INVALID", traversalResult.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(parent.Parent!.FullName, "escaped-export")));
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    [Fact]
    public void ExportRejectsDestinationInsideManagedLogRoot()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        try
        {
            var service = new DiagnosticExportService(source.FullName);
            var result = service.Export(Path.Combine(source.FullName, "nested-export"), Snapshot());

            Assert.Equal(DiagnosticExportStatus.Failed, result.Status);
            Assert.Equal("DESTINATION_NOT_OWNED", result.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(source.FullName, "nested-export")));
        }
        finally
        {
            source.Delete(true);
        }
    }

    [Fact]
    public void OversizedManagedFileProducesBoundedDegradedExport()
    {
        var source = Directory.CreateTempSubdirectory("arcgispro-mcp-export-source-");
        var parent = Directory.CreateTempSubdirectory("arcgispro-mcp-export-parent-");
        try
        {
            var policy = new StructuredLogFilePolicy();
            var manifest = new
            {
                Schema = StructuredLogFilePolicy.ManifestSchema,
                Owner = StructuredLogFilePolicy.Owner,
                FilePrefix = policy.FilePrefix,
                CreatedUtc = DateTimeOffset.UtcNow,
                ManagedFiles = new[] { "mcp-structured-00000000.jsonl" },
                FileCreatedUtc = new Dictionary<string, DateTimeOffset>
                {
                    ["mcp-structured-00000000.jsonl"] = DateTimeOffset.UtcNow
                }
            };
            File.WriteAllText(
                Path.Combine(source.FullName, policy.ManifestFileName),
                JsonSerializer.Serialize(manifest));
            File.WriteAllText(
                Path.Combine(source.FullName, "mcp-structured-00000000.jsonl"),
                new string('x', 1024 * 1024 + 32));

            var destination = Path.Combine(parent.FullName, "bounded-export");
            var result = new DiagnosticExportService(source.FullName).Export(destination, Snapshot());

            Assert.Equal(DiagnosticExportStatus.Degraded, result.Status);
            Assert.Equal("MANAGED_LOG_BOUNDS_EXCEEDED", result.ErrorCode);
            Assert.InRange(new FileInfo(Path.Combine(destination, "managed-log.jsonl")).Length, 1, DiagnosticExportService.MaxManagedLogBytes);
        }
        finally
        {
            source.Delete(true);
            parent.Delete(true);
        }
    }

    private static void WriteManagedRecord(string root, string correlation)
    {
        using var logger = new StructuredFileLogger(root);
        logger.Log(ProductionLogEntryNormalizer.Normalize(new LogEntry
        {
            Level = LogLevel.Information,
            Category = "server",
            Component = "server",
            Operation = "request",
            CorrelationId = correlation,
            MessageCode = "REQUEST_COMPLETED",
            ErrorCode = "NONE",
            Outcome = "SUCCESS",
            DurationMs = 4
        }));
    }

    private static HealthSnapshot Snapshot()
        => new()
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ProductIdentity = "1.0.2",
            OverallStatus = HealthStatus.Running,
            Server = new TransportHealthFacts(
                TransportHealthStatus.Unknown,
                host: "127.0.0.1",
                port: 6520,
                endpoint: "/mcp",
                listenerFactExplicit: false),
            ArcGISHost = new HealthComponent("arcgisHost", HealthComponentStatus.NotChecked, requiredForOverall: false),
            Configuration = new HealthComponent("configuration", HealthComponentStatus.Pass),
            Compatibility = new HealthComponent(
                "compatibility",
                HealthComponentStatus.DeclaredOnly,
                declaredTarget: "3.5.0"),
            Bridge = new HealthComponent("bridge", HealthComponentStatus.NotChecked, requiredForOverall: false),
            Logging = new HealthComponent("logging", HealthComponentStatus.Pass, requiredForOverall: false),
            Clients = new[] { ClientHealthFact.OptionalNotChecked("codex") }
        };

    private static int Count(string value, string token)
        => value.Split(token, StringSplitOptions.None).Length - 1;

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath, out string reason)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or PlatformNotSupportedException
                                   or NotSupportedException
                                   or ArgumentException)
        {
            reason = $"Directory reparse-point test skipped: {ex.GetType().Name}";
            return false;
        }
    }

    private sealed class ReparsePointFactAttribute : FactAttribute
    {
        public ReparsePointFactAttribute()
        {
            if (!CanCreateDirectoryReparsePoint(out var reason))
            {
                Skip = reason;
            }
        }

        private static bool CanCreateDirectoryReparsePoint(out string reason)
        {
            DirectoryInfo? root = null;
            string? link = null;
            try
            {
                root = Directory.CreateTempSubdirectory("arcgispro-mcp-reparse-capability-");
                var target = Directory.CreateDirectory(Path.Combine(root.FullName, "target"));
                link = Path.Combine(root.FullName, "link");
                Directory.CreateSymbolicLink(link, target.FullName);
                if ((File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
                {
                    reason = "Directory reparse-point test skipped: created link was not marked as reparse point.";
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                reason = $"Directory reparse-point test skipped: {ex.GetType().Name}";
                return false;
            }
            finally
            {
                try
                {
                    if (link is not null && Directory.Exists(link))
                    {
                        Directory.Delete(link, recursive: false);
                    }
                }
                catch
                {
                    // Capability probing is best effort; the test-owned root is temporary.
                }

                try
                {
                    root?.Delete(recursive: true);
                }
                catch
                {
                    // Capability probing is best effort; no project data is involved.
                }
            }
        }
    }

    private static string FindCompatibilityAssemblyPath()
    {
        var binRoot = Path.Combine(FindRepositoryRoot(), "Source", "ArcGISProMCP.Compatibility", "bin");
        return Directory
            .EnumerateFiles(binRoot, "ArcGISProMCP.Compatibility.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
