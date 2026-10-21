using System;

namespace WinFormsStubGen;

public class Program
{
    public static int Main(string[] args)
    {
        var options = CommandLineParser.Parse(args);
        if (options == null)
        {
            return 1;
        }

        try
        {
            Console.WriteLine("Generating LiveSplit.Compat.WinForms stub assembly...");
            StubGenerator.Generate(options);
            Console.WriteLine($"Successfully generated LiveSplit.Compat.WinForms in '{options.OutDir}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Generation failed: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }
}

