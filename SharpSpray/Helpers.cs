using System;

namespace A
{
    internal static class Helpers
    {

        public static DateTime? ParseAdDateTime(object obj)
        {
            if (obj == null)
                return null;

            long.TryParse(obj.ToString(), out var lastLogonStr);
            return DateTime.FromFileTime(lastLogonStr);
        }

    }
}
