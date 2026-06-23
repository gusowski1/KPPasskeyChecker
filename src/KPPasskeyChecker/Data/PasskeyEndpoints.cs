namespace KPPasskeyChecker.Data
{
    internal static class PasskeyEndpoints
    {
        private const string BaseUrl = "https://passkeys-api.2fa.directory/v1/";

        public static string ForScope(PasskeyDataScope scope)
        {
            switch (scope)
            {
                case PasskeyDataScope.PasswordlessOnly: return BaseUrl + "passwordless.json";
                case PasskeyDataScope.MfaOnly:          return BaseUrl + "mfa.json";
                default:                                return BaseUrl + "supported.json";
            }
        }

        /// <summary>The detached/inline OpenPGP signed message for a scope (the ".json.sig" file).</summary>
        public static string SignatureForScope(PasskeyDataScope scope)
        {
            return ForScope(scope) + ".sig";
        }

        public static string CacheKey(PasskeyDataScope scope)
        {
            return "passkey_" + scope.ToString();
        }

        /// <summary>
        /// Cache key for PGP-verified data, kept distinct from the unverified key so that toggling
        /// verification never lets unverified cached JSON be served as if it had been verified.
        /// </summary>
        public static string SignedCacheKey(PasskeyDataScope scope)
        {
            return "passkey_" + scope.ToString() + "_signed";
        }
    }
}
