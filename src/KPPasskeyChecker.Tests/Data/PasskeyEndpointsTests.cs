using System;
using KPPasskeyChecker.Data;
using Xunit;

namespace KPPasskeyChecker.Tests.Data
{
    /// <summary>
    /// Full public-surface coverage of <see cref="PasskeyEndpoints"/>, covering the full logic
    /// surface, not just the touched member. Ported 1:1 from
    /// tools\SelfCheck\SelfCheck.cs (CheckScopeEndpointMapping / CheckSignedCacheKeyDistinctness).
    /// </summary>
    public class PasskeyEndpointsTests
    {
        [Theory]
        [InlineData(PasskeyDataScope.PasswordlessOnly, "https://passkeys-api.2fa.directory/v1/passwordless.json")]
        [InlineData(PasskeyDataScope.MfaOnly, "https://passkeys-api.2fa.directory/v1/mfa.json")]
        [InlineData(PasskeyDataScope.AnySupport, "https://passkeys-api.2fa.directory/v1/supported.json")]
        public void ForScope_maps_each_scope_to_its_endpoint(PasskeyDataScope scope, string expected)
        {
            Assert.Equal(expected, PasskeyEndpoints.ForScope(scope));
        }

        [Fact]
        public void ForScope_unknown_enum_value_falls_back_to_supported_json()
        {
            // The switch statement's default branch handles any value outside the three defined
            // members (defensive fail-safe for a widened/miscast enum).
            PasskeyDataScope bogus = (PasskeyDataScope)999;
            Assert.Equal("https://passkeys-api.2fa.directory/v1/supported.json",
                PasskeyEndpoints.ForScope(bogus));
        }

        [Theory]
        [InlineData(PasskeyDataScope.PasswordlessOnly)]
        [InlineData(PasskeyDataScope.MfaOnly)]
        [InlineData(PasskeyDataScope.AnySupport)]
        public void SignatureForScope_appends_dot_sig_to_the_json_endpoint(PasskeyDataScope scope)
        {
            Assert.Equal(PasskeyEndpoints.ForScope(scope) + ".sig", PasskeyEndpoints.SignatureForScope(scope));
        }

        [Theory]
        [InlineData(PasskeyDataScope.PasswordlessOnly, "passkey_PasswordlessOnly")]
        [InlineData(PasskeyDataScope.MfaOnly, "passkey_MfaOnly")]
        [InlineData(PasskeyDataScope.AnySupport, "passkey_AnySupport")]
        public void CacheKey_is_the_scope_name_prefixed_with_passkey_underscore(
            PasskeyDataScope scope, string expected)
        {
            Assert.Equal(expected, PasskeyEndpoints.CacheKey(scope));
        }

        [Theory]
        [InlineData(PasskeyDataScope.PasswordlessOnly, "passkey_PasswordlessOnly_signed")]
        [InlineData(PasskeyDataScope.MfaOnly, "passkey_MfaOnly_signed")]
        [InlineData(PasskeyDataScope.AnySupport, "passkey_AnySupport_signed")]
        public void SignedCacheKey_appends_signed_suffix_to_the_cache_key(
            PasskeyDataScope scope, string expected)
        {
            Assert.Equal(expected, PasskeyEndpoints.SignedCacheKey(scope));
        }

        [Theory]
        [InlineData(PasskeyDataScope.PasswordlessOnly)]
        [InlineData(PasskeyDataScope.MfaOnly)]
        [InlineData(PasskeyDataScope.AnySupport)]
        public void CacheKey_and_SignedCacheKey_are_always_distinct(PasskeyDataScope scope)
        {
            string plain = PasskeyEndpoints.CacheKey(scope);
            string signed = PasskeyEndpoints.SignedCacheKey(scope);

            Assert.NotEqual(plain, signed);
            Assert.EndsWith("_signed", signed, StringComparison.Ordinal);
            Assert.False(plain.EndsWith("_signed", StringComparison.Ordinal));
        }
    }
}
