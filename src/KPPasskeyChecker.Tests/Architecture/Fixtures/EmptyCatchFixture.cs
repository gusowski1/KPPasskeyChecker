using System;

namespace KPPasskeyChecker.Tests.Architecture.Fixtures
{
    /// <summary>
    /// RED-NACHWEIS-FIXTURE (empty-catch guard: leere catch-Bloecke im Produktivcode).
    ///
    /// Diese Datei liegt AUSSCHLIESSLICH im Testprojekt unter Architecture\Fixtures\ und wird NIE
    /// in KPPasskeyChecker.dll/.plgx geshippt. Anders als die uebrigen Fixtures in diesem Ordner
    /// wird sie nicht per Reflection/ArchUnitNET erfasst, sondern per Quelltext-Scan: empty-catch guard ist
    /// ein reiner Text-Scan (leere catch-Bloecke sind IL-Konstrukte, nicht in Metadaten sichtbar,
    /// siehe <see cref="ArchitectureHardeningGuidelines.FindEmptyCatchBlocks"/>). Der ROT-Nachweis
    /// laeuft daher gegen einen Scan, der NUR auf diesen Fixtures-Ordner zeigt (nie gegen den
    /// echten Produktiv-Quellbaum), damit er unabhaengig vom aktuellen Zustand des echten
    /// Produktivcodes zuverlaessig zuschlaegt (kein Bruch gegen echten Code).
    ///
    /// Enthaelt genau einen leeren catch-Block (kein Code, kein Kommentar) als Verletzung.
    /// </summary>
    internal sealed class RogueEmptyCatchType
    {
        public void SwallowSilently()
        {
            try
            {
                ThrowMaybe();
            }
            catch
            {
            }
        }

        private static void ThrowMaybe()
        {
            throw new InvalidOperationException("Fixture exception for the empty-catch RED proof.");
        }
    }
}
