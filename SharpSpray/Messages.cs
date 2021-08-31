using System.Collections.Generic;
using System.Text;

namespace A
{
    internal static class Messages
    {
        public static readonly Dictionary<string, string> ArgsDict = new Dictionary<string, string>
        {
            {"-v", "Show verbose messages."},
            {"-u", "Username list file path. This will be automatically fetched from the active directory if not specified."},
            {"-p", "A single password that will be used to perform the password spray."},
            {"-k --pl", "Password List file path."},
            {"-d", "(Optional) Specify a domain name."},
            {"-m", "Use this option if spraying from a host located outside the Domain context."},
            {"-q --dc-ip", "Domain Controller IP. Required when the option 'm' OutsideDomain is checked"},
            {"-x", "Attempts to exclude disabled accounts from the user list (Not supported with the option -m)"},
            {"-z", "Exclude accounts within 1 attempt of locking out. (Not supported with the option -m)"},
            {"-f", "Custom LDAP filter for users, e.g. \"(description=*admin*)\""},
            {"-o", "A file to output the results to."},
            {"-w", "Do not rely on domain lockout observation window settings and use this specific value. (Default: 32 minute)"},
            {"-s", "(Optional) Delay in seconds between each authentication attempt."},
            {"-j", "(Optional) Jitter in seconds."},
            {"get-users-list", "Get the domain users list from the active directory."},
            {"-Force", "Force start without asking for confirmation."},
            {"--show-examples", "Show usage examples."},
            {"--show-args", "Show command line args."},
        };

        public static void ShowCommandLineArgs()
        {
            

            var sb = new StringBuilder();

            foreach (var kvp in ArgsDict)
            {
                sb.AppendLine($"{kvp.Key}\t{kvp.Value}");
            }

            Messenger.Info(sb.ToString());
        }

        public static void ShowUsageExamples()
        {
            Messenger.Info("SharpSpray.exe -v -x -z --pl password.txt");
            Messenger.Info("SharpSpray.exe -x -z -u users.txt --pl psswd.txt");
            Messenger.Info("SharpSpray.exe -x -z -u users.txt -p Passw0rd!");
            Messenger.Info("SharpSpray.exe -x -z -s 3 -j 1 -u users.txt -k psswd.txt -o sprayed.txt");

            Messenger.Info("SharpSpray.exe -w 32 -d DC-1.local --dc-ip 10.10.20.20 -u users.txt --pl psswd.txt");
            Messenger.Info("SharpSpray.exe -w 32 -s 3 -j 1 -d DC-1.local --dc-ip 10.10.20.20 -u users.txt --pl psswd.txt");

            Messenger.Info("SharpSpray.exe --get-users-list");
            Messenger.Info("SharpSpray.exe --get-users-list > users.txt");
            Messenger.Info("SharpSpray.exe --get-users-list | Out-File -Encoding ascii users.txt");
        }
    }
}
