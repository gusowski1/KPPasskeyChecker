using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using KeeRadar.Shared.Caching;
using KeeRadar.Shared.Http;
using KeeRadar.Shared.Pgp;

namespace KPPasskeyChecker.Data
{
    internal sealed class PasskeyApiClient
    {
        private readonly ConditionalHttpFetcher _fetcher;
        private OpenPgpSignatureVerifier _verifier;

        public PasskeyApiClient(string userAgent)
        {
            _fetcher = new ConditionalHttpFetcher(userAgent);
        }

        public Task<PasskeyDataResult> FetchAsync(
            PasskeyDataScope scope,
            ILocalCache cache,
            bool force = false)
        {
            return FetchAsync(scope, false, cache, force);
        }

        public Task<PasskeyDataResult> FetchAsync(
            PasskeyDataScope scope,
            bool verify,
            ILocalCache cache,
            bool force = false)
        {
            return verify
                ? FetchVerifiedAsync(scope, cache, force)
                : FetchPlainAsync(scope, cache, force);
        }

        private async Task<PasskeyDataResult> FetchPlainAsync(
            PasskeyDataScope scope,
            ILocalCache cache,
            bool force)
        {
            string cacheKey = PasskeyEndpoints.CacheKey(scope);
            string url      = PasskeyEndpoints.ForScope(scope);

            CacheEntry cached = cache.Read(cacheKey);

            FetchResult result = await _fetcher
                .FetchAsync(url, force ? null : cached)
                .ConfigureAwait(false);

            switch (result.Outcome)
            {
                case FetchOutcome.Success:
                {
                    string parseError;
                    PasskeyDirectory dir = TryBuildDirectory(result.Content, out parseError);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse API response: " + parseError);

                    cache.Write(cacheKey, NewEntry(result.Content, result.ETag));
                    return Fresh(dir);
                }

                case FetchOutcome.NotModified:
                    return FromCacheAfterNotModified(cache, cacheKey, cached);

                default:
                    return FallbackOrError(cached, result.ErrorMessage ?? "Unknown error.");
            }
        }

        private async Task<PasskeyDataResult> FetchVerifiedAsync(
            PasskeyDataScope scope,
            ILocalCache cache,
            bool force)
        {
            string cacheKey = PasskeyEndpoints.SignedCacheKey(scope);
            string url      = PasskeyEndpoints.SignatureForScope(scope);

            CacheEntry cached = cache.Read(cacheKey);

            OpenPgpSignatureVerifier verifier;
            try
            {
                verifier = GetVerifier();
            }
            catch (Exception ex)
            {
                return FallbackOrError(cached, "Signature verifier unavailable: " + ex.Message);
            }

            FetchResult result = await _fetcher
                .FetchAsync(url, force ? null : cached, true)
                .ConfigureAwait(false);

            switch (result.Outcome)
            {
                case FetchOutcome.Success:
                {
                    PgpVerificationResult verification = verifier.Verify(result.ContentBytes);
                    if (!verification.IsValid)
                        return FallbackOrError(cached, "PGP verification failed: " + verification.Error);

                    string json = Encoding.UTF8.GetString(verification.SignedContent);
                    string parseError;
                    PasskeyDirectory dir = TryBuildDirectory(json, out parseError);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse verified API response: " + parseError);

                    // Only verified JSON is ever written under the signed cache key.
                    cache.Write(cacheKey, NewEntry(json, result.ETag));
                    return Fresh(dir);
                }

                case FetchOutcome.NotModified:
                    // Cached content under the signed key was verified when it was written.
                    return FromCacheAfterNotModified(cache, cacheKey, cached);

                default:
                    return FallbackOrError(cached, result.ErrorMessage ?? "Unknown error.");
            }
        }

        private OpenPgpSignatureVerifier GetVerifier()
        {
            if (_verifier == null)
                _verifier = DirectoryTrustAnchor.CreateVerifier();
            return _verifier;
        }

        private static PasskeyDataResult FromCacheAfterNotModified(ILocalCache cache, string cacheKey, CacheEntry cached)
        {
            if (cached != null)
            {
                CacheEntry updated = NewEntry(cached.Content, cached.ETag);
                cache.Write(cacheKey, updated);
                cached = updated;
            }

            string parseError;
            PasskeyDirectory dir = cached != null ? TryBuildDirectory(cached.Content, out parseError) : null;
            return dir != null
                ? new PasskeyDataResult { Directory = dir, IsFromCache = true, FetchedAt = cached.FetchedAt }
                : new PasskeyDataResult { ErrorMessage = "Cache missing after 304." };
        }

        private static CacheEntry NewEntry(string content, string etag)
        {
            return new CacheEntry
            {
                Content   = content,
                ETag      = etag,
                FetchedAt = DateTimeOffset.UtcNow
            };
        }

        private static PasskeyDataResult Fresh(PasskeyDirectory dir)
        {
            return new PasskeyDataResult
            {
                Directory   = dir,
                IsFromCache = false,
                FetchedAt   = DateTimeOffset.UtcNow
            };
        }

        private static PasskeyDataResult FallbackOrError(CacheEntry cached, string error)
        {
            if (cached == null)
                return new PasskeyDataResult { ErrorMessage = error };

            string parseError;
            PasskeyDirectory dir = TryBuildDirectory(cached.Content, out parseError);
            if (dir == null)
                return new PasskeyDataResult { ErrorMessage = error };

            return new PasskeyDataResult
            {
                Directory    = dir,
                IsFromCache  = true,
                IsStale      = true,
                FetchedAt    = cached.FetchedAt,
                ErrorMessage = error
            };
        }

        private static PasskeyDirectory TryBuildDirectory(string json, out string error)
        {
            if (string.IsNullOrEmpty(json))
            {
                error = "empty response";
                return null;
            }
            try
            {
                var jss = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
                var raw = jss.Deserialize<Dictionary<string, object>>(json);
                if (raw == null)
                {
                    error = "response did not deserialize to an object";
                    return null;
                }
                error = null;
                return PasskeyDirectory.Build(raw);
            }
            catch (Exception ex)
            {
                // Surface the cause (e.g. malformed JSON or an oversized payload) instead of
                // discarding it silently. The fail-soft cache fallback is unchanged: callers still
                // fall back to cached data and only attach this message for diagnostics.
                error = ex.Message;
                return null;
            }
        }
    }
}
