using System.Security.Cryptography;
using System.Buffers;
using Pastel;

namespace CherryTaleAssetDecryptor;

public readonly record struct DecryptResult(bool Success, string? Error);

public static class Decryptor
{
    private static readonly byte[] SettingKey = [0x34, 0x32, 0x61, 0x75, 0x65, 0x72, 0x31, 0x33]; // 42auer13
    private static readonly byte[] SettingIV = [0x31, 0x32, 0x32, 0x34, 0x37, 0x36, 0x32, 0x36]; // 12247626
    private static readonly byte[] UnityOldVersion = [0x32, 0x30, 0x31, 0x38, 0x2E, 0x33, 0x2E, 0x35, 0x66, 0x31, 0x00]; // 2018.3.5f1
    private static readonly byte[] UnityNewVersion = [0x32, 0x30, 0x32, 0x30, 0x2E, 0x33, 0x2E, 0x34, 0x31, 0x66, 0x31]; // 2020.3.41f1
    private static readonly int[] SwapByteOffsets = [1019, 3481, 6524];
    private static readonly int ScanBufferSize = 64 * 1024;

    static Decryptor()
    {
        if (UnityOldVersion.Length != UnityNewVersion.Length)
        {
            throw new InvalidOperationException("Old and new Unity version strings must be the same length.");
        }
    }

    public static DecryptResult DecryptAssetFile(FileInfo input, FileInfo output)
    {
        input.Refresh();

        string tempPath = output.FullName + ".tmp";

        try
        {
            input.CopyTo(tempPath, overwrite: true);

            int patches;
            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize: 64 * 1024))
            {
                SwapBytes(fs, fs.Length);
                patches = PatchVersion(fs);
            }

            File.Move(tempPath, output.FullName, overwrite: true);
            return new DecryptResult(true, null);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return new DecryptResult(false, ex.Message);
        }
    }

    public static DecryptResult DecryptSettingFile(FileInfo input, FileInfo output)
    {
        try
        {
            byte[] encryptedData = File.ReadAllBytes(input.FullName);

            using DES des = DES.Create();
            des.Key = SettingKey;
            des.IV = SettingIV;
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.PKCS7;

            using ICryptoTransform decryptor = des.CreateDecryptor();
            byte[] decrypted = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

            Console.Write($"Decrypting setting file \"{input.Name}\". ");
            File.WriteAllBytes(output.FullName, decrypted);
            Console.WriteLine("(Success)".Pastel(ConsoleColor.Green));
        }
        catch (Exception e)
        {
            return new DecryptResult(false, e.Message);
        }

        return new DecryptResult(true, null);
    }

    private static void SwapBytes(FileStream fs, long length)
    {
        foreach (int offset in SwapByteOffsets)
        {
            if (length <= offset)
            {
                continue;
            }

            long mirror = length - offset;

            fs.Position = offset;
            int a = fs.ReadByte();
            fs.Position = mirror;
            int b = fs.ReadByte();

            fs.Position = offset;
            fs.WriteByte((byte)b);
            fs.Position = mirror;
            fs.WriteByte((byte)a);
        }
    }

    private static int PatchVersion(FileStream fs)
    {
        int patLen = UnityOldVersion.Length;
        if (fs.Length < patLen)
        {
            return 0;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ScanBufferSize);
        try
        {
            long first = FindVersionOffset(fs, 0, 19, buffer);
            if (first < 0)
            {
                return 0;
            }

            ReplaceVersion(fs, first);
            int replacements = 1;

            long second = FindVersionOffset(fs, first + patLen, long.MaxValue, buffer);
            if (second >= 0)
            {
                ReplaceVersion(fs, second);
                replacements++;
            }

            return replacements;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long FindVersionOffset(FileStream fs, long start, long maxStart, byte[] buffer)
    {
        int patLen = UnityOldVersion.Length;
        long length = fs.Length;
        long pos = start;

        while (pos <= maxStart && pos <= (length - patLen))
        {
            int toRead = (int)Math.Min(buffer.Length, length - pos);
            fs.Position = pos;
            fs.ReadExactly(buffer, 0, toRead);

            int idx = buffer.AsSpan(0, toRead).IndexOf(UnityOldVersion);
            if (idx >= 0)
            {
                long found = pos + idx;
                return found <= maxStart ? found : -1;
            }

            if (toRead < buffer.Length)
            {
                break;
            }

            pos += toRead - (patLen - 1);
        }

        return -1;
    }

    private static void ReplaceVersion(FileStream fs, long pos)
    {
        fs.Position = pos;
        fs.Write(UnityNewVersion, 0, UnityNewVersion.Length);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch { }
    }
}