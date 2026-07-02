using System;
using System.Threading.Tasks;
using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Architecture-Assessment 2026-07-02, Achse 4 / Rangliste #2 ("Fehler-Senke
    /// fuer fire-and-forget-Tasks"). <see cref="PasskeyDirectoryService.Start"/> and
    /// <see cref="PasskeyDirectoryService.ScheduleTimer"/> currently kick off
    /// <c>Task.Run(() =&gt; RefreshAsync(false))</c> without observing the task's outcome. If an
    /// unexpected exception ever escapes <c>RefreshAsync</c> (e.g. from a <c>DataRefreshed</c>
    /// subscriber, or a future code path that does not funnel through the existing
    /// fail-soft/cache-fallback branches), that exception is silently unobserved instead of being
    /// surfaced via <see cref="PasskeyDirectoryService.LastError"/>.
    ///
    /// <see cref="PasskeyDirectoryService"/> itself cannot be unit-tested in isolation for this
    /// concern: its constructor is private (only reachable via the static
    /// <c>Initialize(PasskeySettingsStore, string)</c> factory), and that factory requires a real
    /// <c>KeePass.Plugins.IPluginHost</c> plus network access via the internally-constructed
    /// <c>PasskeyApiClient</c> — there is no injection seam today (Immutable-Core: this test suite
    /// does not force one). Per the task's escape hatch ("falls der Service nicht isoliert
    /// testbar ist, definiere den Test auf der kleinsten beobachtbaren Einheit und benenne die
    /// Grenze"), this test targets the smallest observable unit that the fix requires: a guarded
    /// task-runner ("error sink") that wraps a fire-and-forget <see cref="Func{Task}"/> and writes
    /// any escaping exception into an observable slot instead of leaving it unobserved.
    ///
    /// This type (<c>BackgroundRefreshErrorSink</c>) is wired through <c>PasskeyDirectoryService.Start</c>
    /// / <c>ScheduleTimer</c>.
    /// </summary>
    public class BackgroundRefreshErrorSinkTests
    {
        [Fact]
        public async Task Run_writes_the_exception_message_into_LastError_instead_of_leaving_it_unobserved()
        {
            var sink = new BackgroundRefreshErrorSink();

            await sink.Run(() => { throw new InvalidOperationException("boom"); });

            Assert.Equal("boom", sink.LastError);
        }

        [Fact]
        public async Task Run_writes_the_exception_message_when_the_task_itself_throws_asynchronously()
        {
            var sink = new BackgroundRefreshErrorSink();

            await sink.Run(async () =>
            {
                await Task.Delay(1).ConfigureAwait(false);
                throw new InvalidOperationException("boom-async");
            });

            Assert.Equal("boom-async", sink.LastError);
        }

        [Fact]
        public async Task Run_clears_LastError_on_a_subsequent_successful_run()
        {
            var sink = new BackgroundRefreshErrorSink();

            await sink.Run(() => { throw new InvalidOperationException("first failure"); });
            Assert.Equal("first failure", sink.LastError);

            await sink.Run(() => Task.CompletedTask);

            Assert.Null(sink.LastError);
        }

        [Fact]
        public async Task Run_never_lets_the_exception_propagate_out_of_the_awaited_task()
        {
            var sink = new BackgroundRefreshErrorSink();

            // The whole point of the error sink: callers (fire-and-forget Task.Run sites) must be
            // able to await this without a try/catch of their own and never observe a fault.
            Exception thrown = await Record.ExceptionAsync(
                () => sink.Run(() => { throw new InvalidOperationException("must not propagate"); }));

            Assert.Null(thrown);
        }
    }
}
