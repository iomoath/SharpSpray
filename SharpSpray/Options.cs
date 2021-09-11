using CommandLine;


namespace A
{
    public class Options
    {
        [Option('v', "Verbose", Required = false, HelpText = "Show verbose messages.", Default = false)]
        public bool Verbose { get; set; }


        [Option('u', Required = false, HelpText = "(Optional) Username list file path. This will be automatically fetched from the active directory if not specified.")]
        public string UserListFilePath { get; set; }


        [Option('p', Required = false, HelpText = "A single password that will be used to perform the password spray.")]
        public string Password { get; set; }


        [Option('k', "pl", Required = false, HelpText = "(Optional) Password List file path.")]
        public string PasswordListFilePath { get; set; }


        [Option('d', Group = "OutsideDomain", Required = true, HelpText = "(Optional) Specify a domain name.")]
        public string Domain { get; set; }

        [Option('m', Group = "OutsideDomain", Required = true, HelpText = "Use this option if spraying from a host located outside the Domain context.", Default = false)]
        public bool OutsideDomain { get; set; }

        [Option('q', "dc-ip", Group = "OutsideDomain", Required = true, HelpText = "Required when the option 'm' OutsideDomain is checked")]
        public string DcIp { get; set; }


        [Option('x', Required = false, HelpText = "Attempts to exclude disabled accounts from the user list (Not supported with the option -m)", Default = false)]
        public bool RemoveDisabled { get; set; }


        [Option('z', Required = false, HelpText = "Exclude accounts within 1 attempt of locking out (Not supported with the option -m)", Default = false)]
        public bool RemovePotentialLockouts { get; set; }


        [Option('f', Required = false, HelpText = "Custom LDAP filter for users, e.g. \"(description=*admin*)\"")]
        public string Filter { get; set; }


        [Option('o', Required = false, HelpText = "A file to output the results to.")]
        public string OutFile { get; set; }


        [Option('w', Group = "OutsideDomain", Required = true,  HelpText = "Do not relay on domain lockout observation window settings and use this specific value. (Default 32 minute)", Default = 32)]
        public int DelayBetweenEachSprayAttempt { get; set; }


        [Option('s', Required = false, HelpText = "(Optional) Delay in seconds between each authentication attempt.")]
        public int DelayBetweenEachAuthAttempt { get; set; }


        [Option('j', Required = false, HelpText = "(Optional) Jitter in seconds.")]
        public int Jitter { get; set; }

        [Option("Force", Required = false, HelpText = "Force start without asking for confirmation.")]
        public bool ForceStart { get; set; }

        [Option("get-users-list", Required = false, HelpText = "Get the domain users list from the active directory.")]
        public bool GetUsersList { get; set; }

        [Option("show-examples", Required = false, HelpText = "Show usage examples.")]
        public bool ShowUsageExamples { get; set; }


        [Option("show-args", Required = false, HelpText = "Show command line args")]
        public bool ShowCommandArgs { get; set; }
    }
}
