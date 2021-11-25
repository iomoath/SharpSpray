<div align="center">
    <h1>SharpSpray</h1>
    <br/>
</div>

SharpSpray is a Windows domain password spraying tool written in .NET C#.

## Introduction

SharpSpray is a C# port of [DomainPasswordSpray](https://github.com/dafthack/DomainPasswordSpray) with enhanced and extra capabilities. This tool uses LDAP Protocol to communicate with the Domain active directory services.

### Features
- Can operate from inside and outside a domain context.
- Exclude domain disabled accounts from the spraying.
- Auto gathers domain users from the Active directory.
- Avoid potential lockouts by excluding accounts within one attempt of locking out.
- Avoid potential lockouts by auto-gathering domain lockout observation window settings.
- Compatible with Domain Fine-Grained Password policies.
- Custom LDAP filter for users, e.g. (description=*admin*)
- Delay in seconds between each authentication attempt.
- Jitter between each authentication attempt.
- Support a single password or a list of passwords.
- Single file Console Application.


## Usage

### Command Line Args
```
> SharpSpray.exe --help

  -v, --Verbose       Show verbose messages.
  -u                  (Optional) Username list file path. This will be
                      automatically fetched from the active directory if not specified.
  -p                  A single password or a list splited by '|' that will be used to perform the password spray.
  -k, --pl            (Optional) Password List file path.
  -d                  (Optional) Specify a domain name.
  -m                  Use this option if spraying from a host located outside the Domain context.
  -q, --dc-ip         Required when the option 'm' OutsideDomain is checked
  -x                  Attempts to exclude disabled accounts
                      from the user list (Not supported with the option -m)
  -z                  Exclude accounts within 1 attempt of
                      locking out (Not supported with the option -m)
  -f                  Custom LDAP filter for users, e.g. "(description=*admin*)"
  -o                  A file to output the results to.
  -w                  Do not relay on domain lockout observation window settings and use this specific value. (Default 32 minute)
  -s                  (Optional) Delay in seconds between each authentication attempt.
  -j                  (Optional) Jitter in seconds.
  --Force             Force start without asking for confirmation.
  --get-users-list    Get the domain users list from the active directory.
  --show-examples     Get domain users list from the active directory.
  --show-args         Show command line args
  --help              Display this help screen.
```

### Usage Examples
```
SharpSpray.exe -v -x -z --pl password.txt
SharpSpray.exe -v -x -z -p "Passw0rd|Admin@123|Admin@2022"
SharpSpray.exe -v -x -z -p "Passw0rd"
SharpSpray.exe -x -z -u users.txt --pl psswd.txt
SharpSpray.exe -x -z -u users.txt -p Passw0rd!
SharpSpray.exe -x -z -s 3 -j 1 -u users.txt -k psswd.txt -o sprayed.txt

SharpSpray.exe -w 32 -m -d DC-1.local --dc-ip 10.10.20.20 -u users.txt --pl psswd.txt
SharpSpray.exe -w 32 -s 3 -j 1 -m -d DC-1.local --dc-ip 10.10.20.20 -u users.txt --pl psswd.txt

SharpSpray.exe --get-users-list
SharpSpray.exe --get-users-list > users.txt
PS> .\SharpSpray.exe --get-users-list | Out-File -Encoding ascii users.txt


* Using in Cobalt Strike:
beacon> execute-assembly C:\Users\sec\Desktop\tmp\sharpspray.exe --Force -w 32 -s 3 -j 1 -x -z -v -p "Passw0rd|Admin@123|Admin@2022"

* Check Jobs in Cobalt:
beacon> jobs

```

### Fetching only the users list from the Active Directory

The following command will fetch domain users and prints the list to the console.

```
SharpSpray.exe -x -z --get-users-list

-x: Exclude disabled accounts from the user list.
-z: Exclude accounts within 1 attempt of locking out.
```




### Meta
[SharpSpray | Active Directory Password Spraying Tool](https://c99.sh/sharpspray-active-directory-password-spraying-tool/)