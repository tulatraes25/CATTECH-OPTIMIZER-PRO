using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Simulated pipe completion only: NEVER starts DumpChk or reads real dumps.
/// A descendant retaining a redirected pipe must not hang CATTECH forever.
/// </summary>
public sealed class DumpCheckProcessRunnerDrainTests
{
    [Fact]
    public async Task CompletedPipes_AreAcceptedOnlyWhenBothFinish()
    {
        Assert.True(await DumpCheckProcessRunner.AwaitOutputDrainAsync(
            Task.CompletedTask, Task.CompletedTask,
            TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneNeverEndingPipe_FailsBoundedly_AfterProcessExit(bool holdStdout)
    {
        var stuck = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stdout = holdStdout ? stuck.Task : Task.CompletedTask;
        var stderr = holdStdout ? Task.CompletedTask : stuck.Task;

        // The fake never receives an EOF. A bounded wait must return false,
        // not claim an analyzable result or await the fake indefinitely.
        Assert.False(await DumpCheckProcessRunner.AwaitOutputDrainAsync(
            stdout, stderr, TimeSpan.FromMilliseconds(100),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

        stuck.TrySetResult(true); // Synthetic cleanup of test-owned task.
    }

    [Fact]
    public async Task CancellationDuringPipeDrain_IsNotMisreportedAsSuccess()
    {
        var stuck = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DumpCheckProcessRunner.AwaitOutputDrainAsync(
                stuck.Task, Task.CompletedTask,
                TimeSpan.FromSeconds(5), cancelled.Token));
        stuck.TrySetResult(true);
    }

    [Fact]
    public async Task InvalidDrainLimit_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            DumpCheckProcessRunner.AwaitOutputDrainAsync(
                Task.CompletedTask, Task.CompletedTask,
                TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public async Task OutputReadFault_IsNotSilentlyCertifiedAsCompleted()
    {
        var faulty = Task.FromException(new IOException("synthetic read fault"));
        await Assert.ThrowsAsync<IOException>(() =>
            DumpCheckProcessRunner.AwaitOutputDrainAsync(
                faulty, Task.CompletedTask, TimeSpan.FromSeconds(1),
                CancellationToken.None));
    }
}
