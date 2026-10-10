using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;
    private readonly ILogService _log;

    // 読み込み・保存を 1 つずつ行い、「最新の設定を土台に書き換える」処理や通知の順序が入れ違わないようにする
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private AppSettings _current = new();

    public SettingsService(ISettingsStore store, ILogService log)
    {
        _store = store;
        _log = log;
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event EventHandler<AppSettings>? SettingsChanged;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppSettings loaded;
            try
            {
                loaded = (await _store.LoadAsync(cancellationToken).ConfigureAwait(false) ?? new AppSettings()).Normalize();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warning($"設定の読み込みに失敗したため既定値を使用します: {ex.Message}");
                loaded = new AppSettings();
            }

            Apply(loaded);
            return loaded;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SaveCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> UpdateAsync(
        Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Current;
            var updated = update(current);
            if (updated == current)
                return [];

            return await SaveCoreAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary><see cref="_saveLock"/> を取得した状態で呼ぶこと。</summary>
    private async Task<IReadOnlyList<string>> SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate();
        if (errors.Count > 0)
            return errors;

        var normalized = settings.Normalize();
        try
        {
            await _store.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"設定の保存に失敗しました: {ex.Message}");
            return [$"設定の保存に失敗しました: {ex.Message}"];
        }

        Apply(normalized);
        _log.Info("設定を保存しました。");
        return [];
    }

    private void Apply(AppSettings settings)
    {
        var previous = Interlocked.Exchange(ref _current, settings);
        if (previous != settings)
            SettingsChanged?.Invoke(this, settings);
    }
}
