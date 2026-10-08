using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;
    private readonly ILogService _log;
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

    public async Task<IReadOnlyList<string>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
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
