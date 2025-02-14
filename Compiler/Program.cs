using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Compiler.LangServer;
using Core.Exceptions;
using Core.Generators;
using Core.Logging;
using Core.Meta;

namespace Compiler
{
    internal class Program
    {
        private static CommandLineFlags? _flags;

        private static void WriteHelpText()
        {
            if (_flags is not null)
            {
                DiagnosticLogger.Instance.WriteLine(string.Empty);
                DiagnosticLogger.Instance.WriteLine(_flags.HelpText);
            }
        }

        private static async Task<int> Main()
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            var formatter = CommandLineFlags.FindLogFormatter(Environment.GetCommandLineArgs());
            DiagnosticLogger.Initialize(CommandLineFlags.FindLogFormatter(Environment.GetCommandLineArgs()));
            try
            {
                if (!CommandLineFlags.TryParse(Environment.GetCommandLineArgs(), out _flags, out var message))
                {
                   DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException(message));
                    return BebopCompiler.Err;
                }

                if (_flags.Debug)
                {
                
                    DiagnosticLogger.Instance.WriteLine($"Waiting for debugger to attach (PID={Helpers.ProcessId})...");
                    // Wait 5 minutes for a debugger to attach
                    var timeoutToken = new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token;
                    while (!Debugger.IsAttached)
                    {
                        await Task.Delay(100, timeoutToken);
                    }
                    Debugger.Break();
                }

                if (_flags.Version)
                {
                    DiagnosticLogger.Instance.WriteLine($"{ReservedWords.CompilerName} {DotEnv.Generated.Environment.Version}");
                    return BebopCompiler.Ok;
                }

                if (_flags.Help)
                {
                    WriteHelpText();
                    return BebopCompiler.Ok;
                }

                if (_flags.LanguageServer)
                {
                    await BebopLangServer.RunAsync();
                    return BebopCompiler.Ok;
                }

                var compiler = new BebopCompiler(_flags);


                if (_flags.CheckSchemaFile is not null)
                {
                    if (string.IsNullOrWhiteSpace(_flags.CheckSchemaFile))
                    {
                        DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("No textual schema was read from standard input."));
                        return BebopCompiler.Err;
                    }
                    return await compiler.CheckSchema(_flags.CheckSchemaFile);
                }

                List<string>? paths = null;

                if (_flags.SchemaDirectory is not null)
                {
                    paths = new DirectoryInfo(_flags.SchemaDirectory!)
                        .GetFiles($"*.{ReservedWords.SchemaExt}", SearchOption.AllDirectories)
                        .Select(f => f.FullName)
                        .ToList();
                }
                else if (_flags.SchemaFiles is not null)
                {
                    paths = _flags.SchemaFiles;
                }

                if (_flags.CheckSchemaFiles is not null)
                {
                    if (_flags.CheckSchemaFiles.Count > 0)
                    {
                        return await compiler.CheckSchemas(_flags.CheckSchemaFiles);
                    }
                    // Fall back to the paths defined by the config if none specified
                    else if (paths is not null && paths.Count > 0)
                    {
                        return await compiler.CheckSchemas(paths);
                    }
                     DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("No schemas specified in check."));
                    return BebopCompiler.Err;
                }

                if (!_flags.GetParsedGenerators().Any())
                {
                     DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("No code generators were specified."));
                    return BebopCompiler.Err;
                }

                if (_flags.SchemaDirectory is not null && _flags.SchemaFiles is not null)
                {
                     DiagnosticLogger.Instance.WriteDiagonstic(
                        new CompilerException("Can't specify both an input directory and individual input files"));
                    return BebopCompiler.Err;
                }

                if (_flags.Watch)
                {

                    var watcher = new Watcher(_flags.WorkingDirectory, compiler, _flags.PreserveWatchOutput);

                    if (_flags.WatchExcludeDirectories is not null)
                    {
                        watcher.AddExcludeDirectories(_flags.WatchExcludeDirectories);
                    }
                    if (_flags.WatchExcludeFiles is not null)
                    {
                        watcher.AddExcludeFiles(_flags.WatchExcludeFiles);
                    }
                    return await watcher.StartAsync();
                }

                if (paths is null)
                {
                    DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("Specify one or more input files with --dir or --files."));
                    return BebopCompiler.Err;
                }
                if (paths.Count == 0)
                {
                     DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("No input files were found at the specified target location."));
                    return BebopCompiler.Err;
                }

                // Everything below this point requires paths
                foreach (var parsedGenerator in _flags.GetParsedGenerators())
                {
                    if (!GeneratorUtils.ImplementedGenerators.ContainsKey(parsedGenerator.Alias))
                    {
                         DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException($"'{parsedGenerator.Alias}' is not a recognized code generator"));
                        return BebopCompiler.Err;
                    }
                    if (string.IsNullOrWhiteSpace(parsedGenerator.OutputFile))
                    {
                         DiagnosticLogger.Instance.WriteDiagonstic(new CompilerException("No output file was specified."));
                        return BebopCompiler.Err;
                    }
                    var result = await compiler.CompileSchema(GeneratorUtils.ImplementedGenerators[parsedGenerator.Alias], paths, new FileInfo(parsedGenerator.OutputFile), _flags.Namespace ?? string.Empty, parsedGenerator.Services, parsedGenerator.LangVersion);
                    if (result != BebopCompiler.Ok)
                    {
                        return result;
                    }
                }
                return BebopCompiler.Ok;

            }
            catch (Exception e)
            {
                DiagnosticLogger.Instance.WriteDiagonstic(e);
                return BebopCompiler.Err;
            }
        }

    }
}
