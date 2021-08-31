using System;
using System.Diagnostics;
using System.Threading;

namespace A
{
    internal class LocalCmd
    {
        private readonly object _locker = new object();
        private string _lastCommandOutput;
        private string _lastError;

        public bool RunCmd(string cmd)
        {
            var process = new Process();

            try
            {
                Monitor.Enter(_locker);

                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = " /C " + "\"" + cmd + "\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                var procOutput = process.StandardOutput.ReadToEnd();

                var err = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(err))
                {
                    _lastError = err;
                    return false;
                }

                _lastCommandOutput = procOutput;
                return true;

            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return false;
            }
            finally
            {
                if (!process.HasExited)
                    process.WaitForExit();

                Monitor.Exit(_locker);
            }
        }
   
        public string GetLastError()
        {
            try
            {
                Monitor.Enter(_locker);
                return _lastError;
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }

        public string GetLastCommandOutput()
        {
            try
            {
                Monitor.Enter(_locker);
                return _lastCommandOutput;
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }
    }
}
