namespace CryptoSoft;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: CryptoSoft.exe [filepath] [key]");
            Environment.Exit(-1);
        }

        try
        {
            var fileManager = new FileManager(args[0], args[1]);
            int elapsedTime = fileManager.TransformFile();
            Environment.Exit(elapsedTime);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            Environment.Exit(-99);
        }
    }
}