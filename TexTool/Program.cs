using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace TexTool
{
    internal class Program
    {
        static void PrintHelp()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var procname = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            
            var sb = new StringBuilder();
            sb.AppendLine($"TexTool {version}")
                .AppendLine()
                .AppendLine("Usage:")
                .AppendLine()
                .AppendLine($"{procname}.exe <files and folders>")
                .AppendLine()
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
                Texture.Convert(path);
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

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }
            
            Process(args);
        }
    }
}