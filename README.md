# Cherry Tale Asset Decryptor

A tool for decrypting Cherry Tale asset files.

## Features

- Decrypt Unity asset file
- Decrypt setting file

## Usage

### Command-line Overview

```txt
Description:
  Cherry Tale Asset Decryptor

Usage:
  CherryTaleAssetDecryptor <input> [options]

Arguments:
  <input>  File or directory asset path

Options:
  -f, --file             Process as a single file
  -o, --output <output>  Output file or directory path
  -?, -h, --help         Show help and usage information
  --version              Show version information
```

### Decrypting

This program will process all asset files inside given directory path

```shell
CherryTaleAssetDecryptor ./path/to/directory
```

but if you want to decrypt only a single file just add `-f` / `--file` option

```shell
CherryTaleAssetDecryptor -f ./path/to/file
```

> Decrypted file will saved with "\_output" suffix if you didn't set `-o` / `--output` option.

## Related Projects

- [CherryTale_AssetDecDL](https://github.com/28598519a/CherryTale_AssetDecDL) by 28598519a
