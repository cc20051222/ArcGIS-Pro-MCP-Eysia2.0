using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.UnitTests;

public sealed class TestWorkspaceTests
{
    [Fact]
    public void CreatesMarkedRootAndShortUniqueDatasetNames()
    {
        using var workspace = TestWorkspace.Create();

        Assert.True(Directory.Exists(workspace.RootPath));
        Assert.True(File.Exists(workspace.MarkerPath));

        var first = workspace.GetUniqueDatasetName("Buffer analysis");
        var second = workspace.GetUniqueDatasetName("Buffer analysis");

        Assert.NotEqual(first, second);
        Assert.Matches("^[A-Za-z][A-Za-z0-9_]*$", first);
        Assert.InRange(first.Length, 1, 64);
        Assert.Contains(workspace.RunId, first);
        Assert.Contains(workspace.MarkerPath, workspace.OwnedPaths);

        var cleanup = workspace.Cleanup();
        Assert.True(cleanup.Succeeded, cleanup.FailureMessage);
    }

    [Fact]
    public void CreatesOwnedArtifactsAndLeavesExternalSiblingUntouched()
    {
        using var workspace = TestWorkspace.Create();
        var directory = workspace.CreateOwnedDirectory("owned");
        var file = workspace.CreateOwnedFile(Path.Combine("owned", "payload.txt"), "owned");
        var external = Path.Combine(
            Path.GetDirectoryName(workspace.RootPath)!,
            workspace.RunId + "_external.txt");
        File.WriteAllText(external, "outside");

        try
        {
            Assert.True(workspace.IsOwnedPath(directory));
            Assert.True(workspace.IsOwnedPath(file));
            Assert.False(workspace.IsOwnedPath(external));

            var cleanup = workspace.Cleanup();

            Assert.True(cleanup.Succeeded, cleanup.FailureMessage);
            Assert.False(Directory.Exists(workspace.RootPath));
            Assert.True(File.Exists(external));
        }
        finally
        {
            if (File.Exists(external))
            {
                File.Delete(external);
            }
        }
    }

    [Fact]
    public void RejectsAbsoluteAndEscapingPaths()
    {
        using var workspace = TestWorkspace.Create();

        Assert.Throws<ArgumentException>(() => workspace.CreateOwnedFile("..\\escape.txt"));
        Assert.Throws<ArgumentException>(() => workspace.CreateOwnedDirectory(Path.GetTempPath()));
        Assert.False(workspace.IsOwnedPath(Path.Combine(workspace.RootPath, "not-created.txt")));

        var cleanup = workspace.Cleanup();
        Assert.True(cleanup.Succeeded, cleanup.FailureMessage);
    }

    [Fact]
    public void CleanupReportsUnownedContentWithoutDeletingIt()
    {
        using var workspace = TestWorkspace.Create();
        var unowned = Path.Combine(workspace.RootPath, "not-registered.txt");
        File.WriteAllText(unowned, "not registered by helper");

        try
        {
            var blocked = workspace.Cleanup();

            Assert.False(blocked.Succeeded);
            Assert.Equal(TestWorkspaceCleanupStatus.BlockedByOwnership, blocked.Status);
            Assert.Contains(unowned, blocked.RemainingPaths);
            Assert.True(File.Exists(unowned));
            Assert.True(Directory.Exists(workspace.RootPath));

            Assert.False(workspace.TryRegisterOwnedPath(Path.Combine(workspace.RootPath, "missing.txt")));
            Assert.True(workspace.TryRegisterOwnedPath(unowned));
            Assert.True(workspace.IsOwnedPath(unowned));

            var cleanup = workspace.Cleanup();
            Assert.True(cleanup.Succeeded, cleanup.FailureMessage);
            Assert.False(File.Exists(unowned));
            Assert.False(Directory.Exists(workspace.RootPath));
        }
        finally
        {
            if (File.Exists(unowned))
            {
                File.Delete(unowned);
            }
        }
    }

    [Fact]
    public void CleanupIsIdempotentAfterSuccess()
    {
        using var workspace = TestWorkspace.Create();
        workspace.CreateOwnedFile("payload.txt", "payload");

        var first = workspace.Cleanup();
        var second = workspace.Cleanup();

        Assert.True(first.Succeeded, first.FailureMessage);
        Assert.True(second.Succeeded, second.FailureMessage);
        Assert.Equal(TestWorkspaceCleanupStatus.Succeeded, second.Status);
        Assert.False(Directory.Exists(workspace.RootPath));
    }
}
