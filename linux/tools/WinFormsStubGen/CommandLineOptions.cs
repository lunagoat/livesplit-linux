using System;

namespace WinFormsStubGen;

public static class CommandLineParser
{
    public static GeneratorOptions? Parse(string[] args)
    {
        var options = new GeneratorOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--ref-dir")
            {
                if (i + 1 < args.Length) options.RefDir = args[++i];
                else { Console.Error.WriteLine("Error: Missing value for --ref-dir"); return null; }
            }
            else if (arg == "--core")
            {
                if (i + 1 < args.Length) options.CorePath = args[++i];
                else { Console.Error.WriteLine("Error: Missing value for --core"); return null; }
            }
            else if (arg == "--drawing")
            {
                if (i + 1 < args.Length) options.DrawingPath = args[++i];
                else { Console.Error.WriteLine("Error: Missing value for --drawing"); return null; }
            }
            else if (arg == "--out")
            {
                if (i + 1 < args.Length) options.OutDir = args[++i];
                else { Console.Error.WriteLine("Error: Missing value for --out"); return null; }
            }
            else if (arg == "-h" || arg == "--help")
            {
                PrintUsage();
                return null;
            }
            else
            {
                Console.Error.WriteLine($"Error: Unrecognized option '{arg}'");
                PrintUsage();
                return null;
            }
        }

        if (string.IsNullOrEmpty(options.RefDir) ||
            string.IsNullOrEmpty(options.CorePath) ||
            string.IsNullOrEmpty(options.DrawingPath) ||
            string.IsNullOrEmpty(options.OutDir))
        {
            Console.Error.WriteLine("Error: All options (--ref-dir, --core, --drawing, --out) are required.");
            PrintUsage();
            return null;
        }

        return options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: WinFormsStubGen --ref-dir <dir> --core <path> --drawing <path> --out <dir>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --ref-dir <dir>    Directory containing reference assemblies (System.Windows.Forms.dll, etc.)");
        Console.WriteLine("  --core <path>      Path to LiveSplit.Core.dll");
        Console.WriteLine("  --drawing <path>   Path to LiveSplit.Drawing.dll");
        Console.WriteLine("  --out <dir>        Output directory for LiveSplit.Compat.WinForms.dll");
    }
}
