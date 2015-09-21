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
            System.Console.WriteLine("XML to Table");
            System.Console.WriteLine("Imports and shreds XML files to a SQL Server database");
            System.Console.WriteLine();

            int? exitCode = null;
            CommandLineOptions programSettings = null;
            try
            {
                programSettings = GetStartupOptions(args);
                if (programSettings == null)
                {
                    System.Console.WriteLine(ConsoleResources.UsageMessage);
                    exitCode = ERROR_SUCCESS;
                }

                if (!exitCode.HasValue)
                {
                    if (programSettings.GenerateCreationScript)
                    {
                        try
                        {
                            WriteDatabaseCreationScript(programSettings);
                            exitCode = ERROR_SUCCESS;
                        }
                        catch (Exception fileException)
                        {
                            PrintException(fileException, programSettings);
                            exitCode = GetExitCodeFromFileException(fileException);
                        }
                    }

                    if (!exitCode.HasValue && File.Exists(programSettings.SourceSpecification))
                    {
                        try
                        {
                            programSettings.SourceSpecification = File.ReadAllText(programSettings.SourceSpecification);
                        }
                        catch (Exception fileException)
                        {
                            PrintException(fileException, programSettings);
                            exitCode = GetExitCodeFromFileException(fileException);
                        }
                    }
                }
            }
            catch (Exception settingsException)
            {
                PrintException(settingsException, programSettings);
                exitCode = ERROR_APP_INIT_FAILURE;
            }

            if (!exitCode.HasValue)
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

                        exitCode = ERROR_SUCCESS;
                    }
                }
                catch (Exception engineException)
                {
                    PrintException(engineException, programSettings);
                    exitCode = ERROR_INVALID_DATA;
                }
            }

            System.Console.WriteLine(exitCode.Value == 0 ? "Done!" : "Program execution stopped.");

            if (Debugger.IsAttached)
            {
                System.Console.WriteLine();
                System.Console.Write("Press any key to exit...");
                System.Console.ReadKey();
            }

            return exitCode.Value;
        }

        private static void OnConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs consoleCancelEventArgs)
        {
            consoleCancelEventArgs.Cancel = true;
            _continueShredding = false;
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

        private static void WriteDatabaseCreationScript(CommandLineOptions programSettings)
        {
            AdapterContext context = new AdapterContext(programSettings);
            string creationScript = context.GenerateDatabaseCreationScript();
            File.WriteAllText(programSettings.CreationScriptFilename, creationScript);
        }

        private static int? GetExitCodeFromFileException(Exception fileException)
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
            System.Console.WriteLine();
            System.Console.WriteLine("ERROR:");
            bool verbose = startupOptions != null && startupOptions.Verbose;
            System.Console.WriteLine(verbose ? exception.ToString() : exception.Message);
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
    }
}
