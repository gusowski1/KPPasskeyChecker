using System.Reflection;
using System.Runtime.InteropServices;

// KeePass detects a DLL as a plugin via the file version information block.
// These attributes populate that block. The Product name MUST be exactly
// "KeePass Plugin" or KeePass silently ignores the DLL (no error shown).
// See https://keepass.info/help/v2_dev/plg_index.html
//
// Mapping (KeePass dialog field <- assembly attribute):
//   Title       <- AssemblyTitle        (full plugin name)
//   Description <- AssemblyDescription   (short description)
//   Author      <- AssemblyCompany       (author name)
//   Product     <- AssemblyProduct        (MUST be "KeePass Plugin")
//   Version     <- AssemblyVersion / AssemblyFileVersion (no asterisks!)

[assembly: AssemblyTitle("KPPasskeyChecker")]
[assembly: AssemblyDescription("Checks whether the domain of an entry supports passkeys, using the Passkeys Directory by 2factorauth.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Lars Gusowski")]
[assembly: AssemblyProduct("KeePass Plugin")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("a8e6f3c2-1b4d-4e7a-9c5f-2d3e4f5a6b7c")]

// Plugin version. Keep in sync with PluginVersion.Current ("0.2.0").
// Do NOT use asterisks here (KeePass requires a comparable, fixed version).
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
