using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KPPasskeyChecker.Settings;
using KeeRadar.Shared.Caching;
using KeeRadar.Shared.DomainMatching;
using KeeRadar.Shared.Http;

namespace KPPasskeyChecker.Data
{
    public sealed class PasskeyDirectoryService : IDisposable
    {
        private static PasskeyDirectoryService _current;

        public static bool IsAvailable
        {
            get { return _current != null; }
        }

        public static PasskeyDirectoryService Current
        {
            get
            {
                if (_current == null)
                    throw new InvalidOperationException("PasskeyDirectoryService not initialized.");
                return _current;
            }
        }

        private readonly PasskeyApiClient _client;
        private readonly ILocalCache _cache;
        private readonly PasskeySettingsStore _settings;
        private readonly string _cacheDirectory;
        private readonly BackgroundRefreshErrorSink _backgroundErrorSink = new BackgroundRefreshErrorSink();
        private Timer _refreshTimer;
        private bool _disposed;

        public PasskeyDirectory Directory { get; private set; }
        public DateTimeOffset? LastRefreshed { get; private set; }
        public bool IsStale { get; private set; }
        public bool IsUsingCachedFallback { get; private set; }
        public string LastError { get; private set; }

        public event EventHandler DataRefreshed;

        private PasskeyDirectoryService(PasskeySettingsStore settings, string cacheDirectory)
        {
            _settings = settings;
            _cacheDirectory = cacheDirectory;
            _cache = new FileSystemJsonCache(cacheDirectory);
            _client = new PasskeyApiClient(
                UserAgent.Build("KPPasskeyChecker", PluginVersion.Current, PluginVersion.RepoUrl));
        }

        public static void Initialize(PasskeySettingsStore settings, string cacheDirectory)
        {
            if (_current != null) _current.Dispose();
            _current = new PasskeyDirectoryService(settings, cacheDirectory);
            _current.Start();
        }

        public static void Shutdown()
        {
            if (_current != null) _current.Dispose();
            _current = null;
        }

        private void Start()
        {
            DomainCandidateGenerator.InitializeAsync(_cacheDirectory);
            Task.Run(() => _backgroundErrorSink.Run(() => RefreshAsync(false)));
            ScheduleTimer();
        }

        private void ScheduleTimer()
        {
            long intervalMs = (long)TimeSpan.FromHours(_settings.RefreshIntervalHours).TotalMilliseconds;
            _refreshTimer = new Timer(
                delegate { Task.Run(() => _backgroundErrorSink.Run(() => RefreshAsync(false))); },
                null, intervalMs, intervalMs);
        }

        public async Task RefreshAsync(bool force)
        {
            if (_disposed) return;

            PasskeyDataResult result = await _client
                .FetchAsync(_settings.Scope, _settings.VerifyPgpSignature, _cache, force)
                .ConfigureAwait(false);

            if (_disposed) return;

            Directory             = result.Directory;
            LastRefreshed         = result.FetchedAt;
            IsStale               = result.IsStale;
            IsUsingCachedFallback = result.IsFromCache;
            LastError             = result.ErrorMessage;

            if (DataRefreshed != null)
                DataRefreshed(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }
    }
}
