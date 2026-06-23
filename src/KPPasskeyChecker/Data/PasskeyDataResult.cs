using System;

namespace KPPasskeyChecker.Data
{
    public sealed class PasskeyDataResult
    {
        public PasskeyDirectory Directory { get; set; }
        public bool IsFromCache { get; set; }
        public bool IsStale { get; set; }
        public DateTimeOffset FetchedAt { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsSuccess
        {
            get { return Directory != null; }
        }
    }
}
