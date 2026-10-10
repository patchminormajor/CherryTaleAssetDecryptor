using System.CommandLine;
using Pastel;

namespace CherryTaleAssetDecryptor;

internal static class Program
{
    public static readonly byte[] AssetSignature = [0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53]; // UnityFS

    private static readonly Option<bool> FileModeOption = new("--file", "-f")
    {
        Description = "Process as a single file"
    };

    private static readonly Option<string?> OutputPathOption = new("--output", "-o")
    {
        Description = "Output file or directory path"
    };

    private static readonly Argument<string> InputPathArgument = new("input")
    {
        Description = "File or directory asset path",
        Arity = ArgumentArity.ExactlyOne,
        Validators =
        {
            (result) =>
            {
                var inputPath = result.GetValueOrDefault<string>();

                if (string.IsNullOrEmpty(inputPath))
                {
                    result.AddError("The <input> argument must be set.");
                    return;
                }

                var fileMode = result.GetValue(FileModeOption);
                var fullInputPath = Path.GetFullPath(inputPath);

                if (fileMode && !File.Exists(fullInputPath))
                {
                    result.AddError($"Input file at \"{fullInputPath}\" is not exists.");
                }
                else if (!fileMode && !Directory.Exists(inputPath))
                {
                    result.AddError($"Input directory at \"{fullInputPath}\" is not exists.");
                }
            }
        }
    };

    private static readonly RootCommand CommandInstance = new("Cherry Tale Asset Decryptor")
    {
        InputPathArgument,
        FileModeOption,
        OutputPathOption,
    };

    public static int Main(string[] args)
    {
        CommandInstance.SetAction(parseResult =>
        {
            string? inputPath = parseResult.GetValue(InputPathArgument);
            bool fileMode = parseResult.GetValue(FileModeOption);

            if (inputPath == null)
            {
                Console.Error.WriteLine($"The <input> argument must be set.".Pastel(ConsoleColor.Red));
                return 1;
            }

            string outputPath = GetOutputPath(parseResult);

            if (fileMode)
            {
                return ProcessFile(new FileInfo(inputPath), new FileInfo(outputPath), true);
            }

            return ProcessDirectory(new DirectoryInfo(inputPath), new DirectoryInfo(outputPath));
        });

        return CommandInstance.Parse(args).Invoke();
    }

    public static int ProcessFile(FileInfo file, FileInfo output, bool isFileMode = false)
    {
        if (!IsValidAssetFile(file))
        {
            if (TryDecryptSettingFile(file, output))
            {
                return 0;
            }

            if (isFileMode)
            {
                Console.Error.WriteLine($"Failed to decrypt \"{file.FullName}\": Invalid asset file format.".Pastel(ConsoleColor.Red));
                return 1;
            }

            Console.WriteLine($"Invalid asset or setting file \"{file.Name}\". (Skip)".Pastel(ConsoleColor.Yellow));
            return 1;
        }

        return DecryptAssetFile(file, output);
    }

    private static bool TryDecryptSettingFile(FileInfo input, FileInfo output)
    {
        var result = Decryptor.DecryptSettingFile(input, output);
        return result.Success;
    }

    private static int DecryptAssetFile(FileInfo input, FileInfo output)
    {
        Console.Write($"Decrypting asset file \"{input.Name}\". ");

        try
        {
            if (output.Exists)
            {
                output.Delete();
            }

            var result = Decryptor.DecryptAssetFile(input, output);

            if (!result.Success)
            {
                Console.WriteLine($"(Failed: {result.Error})".Pastel(ConsoleColor.Red));
                return 1;
            }

            Console.WriteLine("(Success)".Pastel(ConsoleColor.Green));
        }
        catch (Exception e)
        {
            Console.WriteLine($"(Failed: {e.Message})".Pastel(ConsoleColor.Red));
            return 1;
        }

        return 0;
    }

    public static int ProcessDirectory(DirectoryInfo directory, DirectoryInfo output)
    {
        Console.WriteLine($"Start decrypting all files in \"{directory.FullName}\"");

        if (output.Exists && output.GetFiles().Length > 0)
        {
            Console.WriteLine($"Output directory is not empty, overwriting!".Pastel(ConsoleColor.Yellow));
            output.Delete(true);
        }

        output.Create();

        foreach (var file in directory.GetFiles())
        {
            var fileOutputPath = Path.Combine(output.FullName, file.Name);
            ProcessFile(file, new FileInfo(fileOutputPath), false);
        }

        Console.WriteLine("Decrypt finished!");

        return 0;
    }

    public static bool IsValidAssetFile(FileInfo file)
    {
        using var fs = file.OpenRead();
        using var reader = new BinaryReader(fs);

        if (file.Length < AssetSignature.Length)
        {
            return false;
        }

        var signature = reader.ReadBytes(AssetSignature.Length);

        return signature.SequenceEqual(AssetSignature);
    }

    public static string GetOutputPath(ParseResult parseResult)
    {
        string inputPath = parseResult.GetRequiredValue(InputPathArgument);
        var outputPath = parseResult.GetValue(OutputPathOption);

        if (!string.IsNullOrEmpty(outputPath))
        {
            return Path.GetFullPath(outputPath);
        }

        return Path.GetFullPath(inputPath) + "_output";
    }
}
