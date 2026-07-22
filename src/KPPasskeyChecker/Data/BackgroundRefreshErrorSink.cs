using System;
using System.Threading.Tasks;

namespace KPPasskeyChecker.Data
{
    /// <summary>
    /// Guards a fire-and-forget background task so an escaping exception is never left unobserved.
    /// <see cref="PasskeyDirectoryService.Start"/> and <see cref="PasskeyDirectoryService.ScheduleTimer"/>
    /// kick off <c>Task.Run(() =&gt; RefreshAsync(false))</c> without awaiting the result; if an
    /// unexpected exception ever escaped <c>RefreshAsync</c> outside its own fail-soft/cache-fallback
    /// branches (e.g. from a <see cref="PasskeyDirectoryService.DataRefreshed"/> subscriber), it would
    /// previously be silently unobserved. This sink funnels that outcome into an observable
    /// <see cref="LastError"/> slot instead, while never propagating the exception to the caller —
    /// callers can <c>await sink.Run(...)</c> without a try/catch of their own.
    ///
    /// This is intentionally separate from <see cref="PasskeyDirectoryService.LastError"/>: that
    /// property already reflects fail-soft outcomes reported by <c>RefreshAsync</c> itself (e.g. a
    /// failed HTTP fetch that fell back to cache) and must not be reinterpreted here. This sink only
    /// covers the previously-unobserved fire-and-forget edge around the <c>Task.Run</c> call sites.
    /// </summary>
    public sealed class BackgroundRefreshErrorSink
    {
        public string LastError { get; private set; }

        /// <summary>
        /// Invokes <paramref name="action"/> and awaits the returned task. Any exception — thrown
        /// synchronously while building the task, or surfaced asynchronously while awaiting it — is
        /// caught, its message recorded in <see cref="LastError"/>, and never rethrown. A successful
        /// run clears <see cref="LastError"/>.
        /// </summary>
        public async Task Run(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }
    }
}
