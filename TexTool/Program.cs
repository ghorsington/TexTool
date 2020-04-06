using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Mono.Options;

namespace TexTool
{
    internal class Program
    {
        private static bool overwrite = false;

        private static OptionSet options = new OptionSet
        {
            {"o", "overwrites files instead of backing them up", ow =>
            {
                if (ow != null)
                    overwrite = true;
            }}
        };
        
        static void PrintHelp()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var procname = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            
            using var sw = new StringWriter();
            options.WriteOptionDescriptions(sw);
            
            var sb = new StringBuilder();
            sb.AppendLine($"TexTool {version}")
                .AppendLine()
                .AppendLine("Usage:")
                .AppendLine()
                .AppendLine($"{procname}.exe <files and folders>")
                .AppendLine()
                .AppendLine($"You can specify the following parameters by adding them inside parentheses of the filename, like {procname}(-o).exe:")
                .AppendLine(sw.ToString())
                .AppendLine("You can also drag-and-drop folder/files onto the tool.")
                .AppendLine()
                .AppendLine("How it works:")
                .AppendLine()
                .AppendLine("All provided .tex files are converted to .png.")
                .AppendLine("All provided valid image files(png, jpg, etc.) are converted to PNG and saved as .tex version 1010.");
            MessageBox.Show(sb.ToString(), "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static void Process(string[] args)
        {
            foreach (var path in args)
            {
                if(File.Exists(path))
                    ProcessFile(path);
                else if (Directory.Exists(path))
                    ProcessDirectory(path);
                else
                    Console.WriteLine($"\"{path}\" is not a valid path!");
            }
        }

        private static void ProcessDirectory(string path)
        {
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                ProcessFile(dir);
        }

        private static void ProcessFile(string path)
        {
            try
            {
                Texture.Convert(path, overwrite);
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine($"Skipping {path}: Not an image (or format not supported)");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot parse {path}: {e.Message}");
            }
        }

        private static void ProcessOptions()
        {
            var procName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            if (!procName.EndsWith(")"))
                return;

            var first = procName.IndexOf("(", StringComparison.InvariantCultureIgnoreCase);

            if (first == -1)
                return;
            
            var argsString = procName.Substring(first + 1, procName.Length - first - 2);

            if (string.IsNullOrEmpty(argsString))
                return;
            
            var args = new List<string>();
            var sb = new StringBuilder();
            var quoteMode = false;
            var escapeNext = false;
            
            foreach (var c in argsString)
            {
                if (escapeNext)
                {
                    sb.Append(c);
                    escapeNext = false;
                    continue;
                } 
                
                switch (c)
                {
                    case '\\':
                        sb.Append(c);
                        escapeNext = true;
                        break;
                    case ' ' when !quoteMode:
                    {
                        if (sb.Length != 0)
                        {
                            args.Add(sb.ToString());
                            sb.Clear();
                        }

                        break;
                    }
                    case '"':
                        sb.Append(c);
                        quoteMode = !quoteMode;
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            args.Add(sb.ToString());

            options.Parse(args);
        }
        
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            ProcessOptions();
            Process(args);
        }
    }
}