using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using KPPasskeyChecker.Shared.Caching;
using KPPasskeyChecker.Shared.Http;
using KPPasskeyChecker.Shared.Pgp;

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
                    PasskeyDirectory dir = TryBuildDirectory(result.Content);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse API response.");

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
                    PasskeyDirectory dir = TryBuildDirectory(json);
                    if (dir == null)
                        return FallbackOrError(cached, "Failed to parse verified API response.");

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
                _verifier = PasskeyTrustAnchor.CreateVerifier();
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

            PasskeyDirectory dir = cached != null ? TryBuildDirectory(cached.Content) : null;
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

            PasskeyDirectory dir = TryBuildDirectory(cached.Content);
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

        private static PasskeyDirectory TryBuildDirectory(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var jss = new JavaScriptSerializer { MaxJsonLength = 50 * 1024 * 1024 };
                var raw = jss.Deserialize<Dictionary<string, object>>(json);
                return raw == null ? null : PasskeyDirectory.Build(raw);
            }
            catch
            {
                return null;
            }
        }
    }
}
