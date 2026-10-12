namespace IPFMMS.Models
{
    public static class AccountStatus
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Deleted = "Deleted";

        public static bool CanLogin(string status)
        {
            return status == Active;
        }

        public static bool CanResumeRegistration(string status)
        {
            return status == Pending;
        }
    }
}