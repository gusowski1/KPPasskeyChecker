namespace KPPasskeyChecker.Data
{
    // Absence of a field in the API means "not documented" — never model as a 3rd member.
    public enum PasskeySupportLevel
    {
        Allowed,
        Required
    }
}
