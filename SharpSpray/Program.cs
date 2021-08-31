using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace A
{
    internal class Program
    {

        #region Data Members

        private static Sb _sb;

        #endregion

        #region Main

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += Abort;
            Messenger.MessageAvailable += OnMessageAvailable;

            if (args.Length == 0)
            {
                PrintAbout();
                Messages.ShowCommandLineArgs();
                Console.WriteLine();
                Messages.ShowUsageExamples();
                return;
            }

            //args = "-v --get-users-list".Split(' ');

            var options = ParseUserArgs(args);
            if (options == null)
                return;
        
            HandleCommand(options);
        }

        #endregion

        #region Parse Command

        private static Options ParseUserArgs(string[] args)
        {
            // Parse arguments passed
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.CaseSensitive = false;
                with.HelpWriter = null;
            });


            Options options = null;


            var parserResult = parser.ParseArguments<Options>(args);
            parserResult.WithParsed(o => { options = o; }).WithNotParsed(errs => DisplayHelp(parserResult, errs));

            return options;
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.AutoVersion = false;
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);

            Messenger.Info(helpText);
        }


        #endregion


        #region Handle Command

        private static void HandleCommand(Options options)
        {
            if (options.ShowCommandArgs)
            {
                PrintAbout();
                Messages.ShowCommandLineArgs();
                return;
            }
            if (options.ShowUsageExamples)
            {
                PrintAbout();
                Messages.ShowUsageExamples();
                return;
            }

            if (options.GetUsersList)
            {
                try
                {

                    var users = new Sb().FetchUerList(options)?.ChunkBy(30);

                    if (users == null)
                        return;

                    foreach (var list in users)
                    {
                        var s = string.Join("\n", list.ToArray());
                        Messenger.Info(s);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Environment.Exit(1);
                }
                finally
                {
                    Environment.Exit(1);
                }
            }


            try
            {
                _sb = new Sb();

                // Init
                var initOk = _sb.Init(options);
                if (!initOk)
                    return;


                // Ask user for confirmation
                if (!options.ForceStart && !GetUserConfirmation())
                    return;

                // Start
                _sb.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                Environment.Exit(1);
            }
        }

        #endregion

      
        #region Handle Keyboard Events

        private static bool GetUserConfirmation()
        {
            Console.WriteLine();

            ConsoleKey response;
            do
            {
                Console.Write("Start password spraying? [y/n] ");
                response = Console.ReadKey(false).Key; // true is intercept key (dont show), false is show
                if (response != ConsoleKey.Enter)
                    Console.WriteLine();
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return response == ConsoleKey.Y;

        }

        private static void Abort(object sender, EventArgs e)
        {
            try
            {
                _sb?.Abort();
            }
            catch
            {
                //
            }
        }

        #endregion

        #region Handle Events

        private static void OnMessageAvailable(object sender, MessageData e)
        {
            //if (Console.BackgroundColor == ConsoleColor.Black)
            //{
            //    Console.ForegroundColor = e.ForegroundColor;
            //}

            Console.ForegroundColor = e.ForegroundColor;

            Console.WriteLine(e.Text);
            Console.ResetColor();
        }

        #endregion

        #region Print Version About Text

        private static void PrintAbout()
        {
            if (Console.BackgroundColor == ConsoleColor.Black)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            foreach (var s in SharedGlobals.About)
            {
                Console.SetCursorPosition((Console.WindowWidth - s.Length) / 2, Console.CursorTop);
                Console.WriteLine(s);
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        #endregion

    }
}