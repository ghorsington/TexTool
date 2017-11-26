using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TexTool
{
    public class Program
    {
        private const string OUTPUT_DIR = "output";
        private static string ProcessName => System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        private static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            Process(args);
        }

        private static void Process(string[] paths)
        {
            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            foreach (string path in paths)
                if (File.Exists(path))
                    ProcessFile(path);
                else if (Directory.Exists(path))
                    ProcessDirectory(path);
                else
                    Console.WriteLine($"[WARN] {path} is not a valid file nor directory (does it exist? can it be accessed?)");
        }

        private static void ProcessFile(string file)
        {
            Texture tex;
            string fileName = Path.GetFileNameWithoutExtension(file);
            string ext = Path.GetExtension(file);
            try
            {
                tex = Texture.Open(file);
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine($"Skipping {fileName}{ext}: Not an image (or format not supported)");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[FAIL] Cannot parse {fileName}.{ext}: {e.Message}");
                return;
            }

            string newExt = ext == Texture.TEX_EXTENSION ? ".png" : ".tex";
            string newPath = Path.Combine(OUTPUT_DIR, $"{fileName}{newExt}");

            try
            {
                tex.Save(newPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[FAIL] Failed to save {fileName}.{newExt}: {e.Message}");
            }
            tex.Dispose();
        }

        private static void ProcessDirectory(string path)
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                ProcessFile(file);
        }

        private static void PrintHelp()
        {
            string help;
            Assembly ass = Assembly.GetExecutingAssembly();
            using (Stream s = ass.GetManifestResourceStream("TexTool.help.txt"))
                using (StreamReader sr = new StreamReader(s))
                    help = sr.ReadToEnd();
            MessageBox.Show(string.Format(help, Version, ProcessName), "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}