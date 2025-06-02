using System.Diagnostics;
using System.Text;

namespace CryptoSoft;

/// <summary>
/// File manager class
/// This class is used to encrypt and decrypt files
/// </summary>
public class FileManager(string path, string key)
{
    private string FilePath { get; } = path;
    private string Key { get; } = key;

    /// <summary>
    /// check if the file exists
    /// </summary>
    private bool CheckFile()
    {
        if (File.Exists(FilePath))
            return true;

        Console.WriteLine("File not found.");
        Thread.Sleep(1000);
        return false;
    }

    /// <summary>
    /// Encrypts the file with xor encryption
    /// </summary>
    public int TransformFile()
    {
        if (!CheckFile()) return -1;

        // Use file streams instead of ReadAllBytes
        string tempOutput = Path.GetTempFileName();
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (var input = File.OpenRead(FilePath))
            using (var output = File.Create(tempOutput))
            {
                var keyBytes = Encoding.UTF8.GetBytes(Key);
                byte[] buffer = new byte[4096];
                int bytesRead;
                int keyIndex = 0;

                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        buffer[i] = (byte)(buffer[i] ^ keyBytes[keyIndex % keyBytes.Length]);
                        keyIndex++;
                    }
                    output.Write(buffer, 0, bytesRead);
                }
            }

            // Replace original only after successful encryption
            File.Replace(tempOutput, FilePath, null);
            stopwatch.Stop();
            return (int)stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            SafeDelete(tempOutput);
            return -99;
        }
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Ignore deletion errors */ }
    }
    /// <summary>
    /// Convert a string in byte array
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static byte[] ConvertToByte(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>
    /// </summary>
    /// <param name="fileBytes">Bytes of the file to convert</param>
    /// <param name="keyBytes">Key to use</param>
    /// <returns>Bytes of the encrypted file</returns>
    private static byte[] XorMethod(IReadOnlyList<byte> fileBytes, IReadOnlyList<byte> keyBytes)
    {
        var result = new byte[fileBytes.Count];
        for (var i = 0; i < fileBytes.Count; i++)
        {
            result[i] = (byte)(fileBytes[i] ^ keyBytes[i % keyBytes.Count]);
        }

        return result;
    }
}
