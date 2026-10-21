using Microsoft.Extensions.Caching.Memory;

namespace IPFMMS.Services
{
    public class LoginAttemptService
    {
        private readonly IMemoryCache _cache;

        private const int MaxAttempts = 5;

        private static readonly TimeSpan AttemptWindow =
            TimeSpan.FromMinutes(10);

        private static readonly TimeSpan LockoutDuration =
            TimeSpan.FromMinutes(2);

        public LoginAttemptService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string GetKey(string email)
        {
            return "login_attempt:" +
                email.Trim().ToLowerInvariant();
        }

        public TimeSpan GetRemainingLockout(string email)
        {
            string key = GetKey(email);

            if (!_cache.TryGetValue(
                    key, out LoginAttemptRecord? record)
                || record == null)
            {
                return TimeSpan.Zero;
            }

            if (record.LockedUntilUtc.HasValue)
            {
                TimeSpan remaining =
                    record.LockedUntilUtc.Value - DateTime.UtcNow;

                if (remaining > TimeSpan.Zero)
                    return remaining;
            }

            return TimeSpan.Zero;
        }

        public void RecordFailedAttempt(string email)
        {
            string key = GetKey(email);

            _cache.TryGetValue(
                key, out LoginAttemptRecord? record);

            record ??= new LoginAttemptRecord();

            if (record.LockedUntilUtc.HasValue &&
                record.LockedUntilUtc.Value <= DateTime.UtcNow)
            {
                record = new LoginAttemptRecord();
            }

            if (record.LockedUntilUtc.HasValue &&
                record.LockedUntilUtc.Value > DateTime.UtcNow)
            {
                return;
            }

            record.FailedAttempts++;

            if (record.FailedAttempts >= MaxAttempts)
            {
                record.LockedUntilUtc =
                    DateTime.UtcNow.Add(LockoutDuration);
            }

            _cache.Set(
                key,
                record,
                AttemptWindow);
        }

        public void ClearAttempts(string email)
        {
            _cache.Remove(GetKey(email));
        }

        private sealed class LoginAttemptRecord
        {
            public int FailedAttempts { get; set; }

            public DateTime? LockedUntilUtc { get; set; }
        }
    }
}