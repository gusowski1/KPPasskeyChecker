using KeeRadar.Shared.Http;
using Xunit;

namespace KPPasskeyChecker.Tests.Shared.Http
{
    /// <summary>
    /// Full public-surface coverage of <see cref="UserAgent"/> — the only member, <c>Build</c>,
    /// incl. edge cases with empty/null parts. Format per CLAUDE.md ("User-Agent format:
    /// {PluginName}/{Version} (+{GitHubRepoURL})"). Ownership: <c>KeeRadar.Shared.*</c> is tested
    /// exclusively in KPPasskeyChecker.Tests (the canonical source); KP2FAChecker.Tests excludes
    /// the whole namespace.
    /// </summary>
    public class UserAgentTests
    {
        [Fact]
        public void Build_composes_name_slash_version_space_plus_repoUrl_in_parentheses()
        {
            string result = UserAgent.Build("KPPasskeyChecker", "0.5.0", "https://github.com/gusowski1/KPPasskeyChecker");

            Assert.Equal(
                "KPPasskeyChecker/0.5.0 (+https://github.com/gusowski1/KPPasskeyChecker)",
                result);
        }

        [Fact]
        public void Build_with_empty_pluginName_still_composes_the_remaining_parts()
        {
            string result = UserAgent.Build(string.Empty, "1.0.0", "https://example.com/repo");

            Assert.Equal("/1.0.0 (+https://example.com/repo)", result);
        }

        [Fact]
        public void Build_with_empty_version_still_composes_the_remaining_parts()
        {
            string result = UserAgent.Build("Plugin", string.Empty, "https://example.com/repo");

            Assert.Equal("Plugin/ (+https://example.com/repo)", result);
        }

        [Fact]
        public void Build_with_empty_repoUrl_still_produces_empty_parentheses_marker()
        {
            string result = UserAgent.Build("Plugin", "1.0.0", string.Empty);

            Assert.Equal("Plugin/1.0.0 (+)", result);
        }

        [Fact]
        public void Build_with_all_parts_empty_yields_the_bare_separators()
        {
            string result = UserAgent.Build(string.Empty, string.Empty, string.Empty);

            Assert.Equal("/ (+)", result);
        }

        [Fact]
        public void Build_with_null_pluginName_concatenates_the_literal_null_marker()
        {
            // string + null yields string.Empty via string concatenation semantics (no NRE) —
            // pins the actual (non-guarded) behaviour of the production code.
            string result = UserAgent.Build(null, "1.0.0", "https://example.com");

            Assert.Equal("/1.0.0 (+https://example.com)", result);
        }

        [Fact]
        public void Build_with_null_version_concatenates_without_throwing()
        {
            string result = UserAgent.Build("Plugin", null, "https://example.com");

            Assert.Equal("Plugin/ (+https://example.com)", result);
        }

        [Fact]
        public void Build_with_null_repoUrl_concatenates_without_throwing()
        {
            string result = UserAgent.Build("Plugin", "1.0.0", null);

            Assert.Equal("Plugin/1.0.0 (+)", result);
        }

        [Fact]
        public void Build_with_all_parts_null_yields_the_bare_separators()
        {
            string result = UserAgent.Build(null, null, null);

            Assert.Equal("/ (+)", result);
        }
    }
}
