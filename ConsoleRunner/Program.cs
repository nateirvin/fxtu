using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using XmlToTable.Console.Properties;
using XmlToTable.Core;

namespace XmlToTable.Console
{
    internal class Program
    {
        //https://msdn.microsoft.com/en-us/library/ms681382(v=vs.85)
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_PATH_NOT_FOUND = 3;
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_INVALID_DATA = 13;
        private const int ERROR_READ_FAULT = 30;
        private const int ERROR_APP_INIT_FAILURE = 575;

        private static bool? _continueShredding;

        private static int Main(string[] args)
        {
            System.Console.CancelKeyPress += OnConsoleCancelKeyPress;

            PrintHeader();

            int exitCode = ERROR_SUCCESS;
            CommandLineOptions programSettings = null;
            try
            {
                programSettings = GetStartupOptions(args);
                if (programSettings == null)
                {
                    System.Console.WriteLine(ConsoleResources.UsageMessage);
                }
                else
                {
                    if (!HandleScriptOutputFlow(programSettings))
                    {
                        programSettings.SourceSpecification = GetSpecification(programSettings.SourceSpecification);
                        HandleShredding(programSettings);
                    }
                }
            }
            catch (IOException fileException)
            {
                PrintException(fileException, programSettings);
                exitCode = GetExitCodeFromFileException(fileException);
            }
            catch (ShreddingException engineException)
            {
                PrintException(engineException, programSettings);
                exitCode = ERROR_INVALID_DATA;   
            }
            catch (Exception generalException)
            {
                PrintException(generalException, programSettings);
                exitCode = ERROR_APP_INIT_FAILURE;
            }

            return HandleApplicationClose(exitCode);
        }

        private static void OnConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs consoleCancelEventArgs)
        {
            consoleCancelEventArgs.Cancel = true;
            _continueShredding = false;
        }

        private static void PrintHeader()
        {
            System.Console.WriteLine("XML to Table");
            System.Console.WriteLine("Imports and shreds XML files to a SQL Server database");
            System.Console.WriteLine();
        }

        private static CommandLineOptions GetStartupOptions(string[] commandLineArguments)
        {
            CommandLineOptions startupOptions = null;

            string firstArgument = commandLineArguments.FirstOrDefault();
            if (firstArgument == null)
            {
                if (CommandLineOptions.HasConfig)
                {
                    startupOptions = new CommandLineOptions();
                }
            }
            else
            {
                if (commandLineArguments.Length != 1 || !firstArgument.ToLower().IsOneOf("/?", "-h", "-help"))
                {
                    Hashtable rawSettings = new Hashtable();
                    foreach (string commandLineArgument in commandLineArguments)
                    {
                        string[] parts = Regex.Split(commandLineArgument, "[=:]");
                        string firstPart = parts[0];
                        string key = Regex.Replace(firstPart, "[-/!]", String.Empty).Trim();
                        string value;
                        if (parts.Length == 1)
                        {
                            value = firstPart.Contains("!") ? false.ToString() : true.ToString();
                        }
                        else
                        {
                            value = commandLineArgument.Remove(0, firstPart.Length + 1);
                        }
                        rawSettings.Add(key, value);
                    }

                    startupOptions = new CommandLineOptions(rawSettings);
                }
            }

            return startupOptions;
        }

        private static bool HandleScriptOutputFlow(CommandLineOptions programSettings)
        {
            if (programSettings.GenerateCreationScript || programSettings.GenerateUpgradeScript)
            {
                AdapterContext context = new AdapterContext(programSettings);
                string script = programSettings.GenerateCreationScript
                    ? context.GenerateDatabaseCreationScript()
                    : context.GenerateDatabaseUpgradeScript();
                string filename = programSettings.GenerateCreationScript
                    ? programSettings.CreationScriptFilename
                    : programSettings.UpgradeScriptFilename;

                if (!string.IsNullOrWhiteSpace(script))
                {
                    try
                    {
                        File.WriteAllText(filename, script);
                    }
                    catch (Exception mainException)
                    {
                        throw new IOException(null, mainException);
                    }
                }
                else
                {
                    System.Console.WriteLine("No upgrade necessary.");
                }

                return true;
            }

            return false;
        }

        private static string GetSpecification(string rawSpecification)
        {
            if (IsFile(rawSpecification))
            {
                try
                {
                    return File.ReadAllText(rawSpecification);
                }
                catch (Exception mainException)
                {
                    throw new IOException(null, mainException);
                }
            }
            
            return rawSpecification;
        }

        private static bool IsFile(string sourceSpecification)
        {
            return File.Exists(sourceSpecification);
        }

        private static void HandleShredding(CommandLineOptions programSettings)
        {
            try
            {
                using (ShreddingEngine engine = new ShreddingEngine(programSettings))
                {
                    engine.OnProgressChanged += ShowProgress;
                    engine.Initialize();

                    if (programSettings.Repeat)
                    {
                        System.Console.WriteLine("** Press CTRL+C to stop early **");
                    }

                    do
                    {
                        int processedCount = engine.Shred(programSettings.BatchSize);
                        if (processedCount == 0)
                        {
                            _continueShredding = false;
                        }
                        _continueShredding = _continueShredding.GetValueOrDefault(programSettings.Repeat);
                    } while (_continueShredding.Value);

                }
            }
            catch (Exception engineException)
            {
                throw new ShreddingException(engineException);
            }
        }

        private static int GetExitCodeFromFileException(Exception fileException)
        {
            if (fileException is SecurityException || fileException is UnauthorizedAccessException)
            {
                return ERROR_ACCESS_DENIED;
            }

            if (fileException is IOException)
            {
                return ERROR_READ_FAULT;
            }

            return ERROR_PATH_NOT_FOUND;
        }

        private static void PrintException(Exception exception, CommandLineOptions startupOptions)
        {
            TextWriter outputStream = System.Console.Error;
            bool verbose = startupOptions != null && startupOptions.Verbose;

            outputStream.WriteLine();
            outputStream.WriteLine("ERROR:");
            outputStream.WriteLine(verbose ? exception.ToString() : exception.Message);
        }

        private static void ShowProgress(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            string message = progressChangedEventArgs.UserState.ToString();
            if (progressChangedEventArgs.ProgressPercentage == 0)
            {
                System.Console.WriteLine(@"{0}...", message);
            }
            else
            {
                // ReSharper disable once LocalizableElement
                System.Console.Write("\r{0}...               ", message);

                if (progressChangedEventArgs.ProgressPercentage == 100)
                {
                    System.Console.WriteLine();
                }
            }
        }

        private static int HandleApplicationClose(int exitCode)
        {
            System.Console.WriteLine(exitCode == 0 ? "Done!" : "Program execution stopped due to error.");

            if (Debugger.IsAttached)
            {
                System.Console.WriteLine();
                System.Console.Write("Press any key to exit...");
                System.Console.ReadKey();
            }

            return exitCode;
        }
    }
}
