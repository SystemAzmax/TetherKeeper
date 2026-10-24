using System.Text.Json;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Infrastructure.Settings;

/// <summary>
/// 通信量の累計を JSON ファイルに保存する。書き込みは一時ファイル経由で置き換え、途中終了による破損を防ぐ。
/// 動作中は定期的に書き換えるため、設定ファイルとは別のファイルにする。
/// </summary>
public sealed class JsonFileDataUsageStore : IDataUsageStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public JsonFileDataUsageStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public async Task<DataUsage?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
                return null;

            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<DataUsage>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveAsync(DataUsage usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = FilePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, usage, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, FilePath, overwrite: true);
        }
        finally
        {
            _ioLock.Release();
        }
    }
}
