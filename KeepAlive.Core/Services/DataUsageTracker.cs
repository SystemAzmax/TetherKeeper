using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 通信量の累計を管理する。計測 (<see cref="Measurement"/>) で得た増加分を加え、一定間隔で保存する。
/// </summary>
public sealed class DataUsageTracker : IDataUsageTracker
{
    private readonly IDataUsageMeter _meter;
    private readonly IDataUsageStore _store;
    private readonly ILogService _log;
    private readonly TimeProvider _timeProvider;
    private readonly KeepAliveOptions _options;

    private readonly object _gate = new();

    // 保存を 1 つずつ行い、古い累計で新しい累計を上書きしないようにする
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private DataUsage _current = DataUsage.Empty;

    /// <summary>保存済みの累計を読み込んだ (またはリセットした) か。読み込む前に保存すると、保存済みの累計を消してしまう。</summary>
    private bool _loaded;

    /// <summary>保存していない変更があるか。</summary>
    private bool _dirty;

    private DateTimeOffset _lastSavedAt = DateTimeOffset.MinValue;

    public DataUsageTracker(
        IDataUsageMeter meter,
        IDataUsageStore store,
        ILogService log,
        TimeProvider timeProvider,
        KeepAliveOptions options)
    {
        _meter = meter;
        _store = store;
        _log = log;
        _timeProvider = timeProvider;
        _options = options;
    }

    public DataUsage Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<DataUsage>? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        DataUsage? stored;
        try
        {
            stored = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warning($"通信量の累計を読み込めなかったため、0 から数えます: {ex.Message}");
            stored = null;
        }

        DataUsage loaded;
        lock (_gate)
        {
            if (_loaded)
                return;
            _loaded = true;

            // 読み込む前に計測した分 (起動直後にトレイから開始した場合など) も失わないよう加える。
            // 手編集などで負の値になっていた場合は 0 とみなす
            var storedBytes = Math.Max(0, stored?.TotalBytes ?? 0);
            loaded = _current = new DataUsage(storedBytes + _current.TotalBytes, stored?.Since ?? _current.Since);
        }

        RaiseChanged(loaded);
    }

    public async Task ResetAsync()
    {
        DataUsage previous;
        DataUsage reset;
        lock (_gate)
        {
            previous = _current;
            reset = _current = new DataUsage(0, _timeProvider.GetLocalNow());
            // 読み込みより先にリセットした場合も、保存済みの累計を後から加えない
            _loaded = true;
            _dirty = true;
        }

        _log.Info($"通信量の累計 ({DataUsage.Format(previous.TotalBytes)}) をリセットしました。");
        RaiseChanged(reset);
        await SaveAsync().ConfigureAwait(false);
    }

    public IDataUsageMeasurement StartMeasurement()
    {
        DataUsage? started = null;
        lock (_gate)
        {
            // 一度もリセットしていなければ、初めて計測した日時を数え始めた日時とする
            if (_current.Since is null)
            {
                started = _current = _current with { Since = _timeProvider.GetLocalNow() };
                _dirty = true;
            }
        }

        if (started is not null)
            RaiseChanged(started);

        return new Measurement(this);
    }

    private void Add(long bytes)
    {
        if (bytes <= 0)
            return;

        DataUsage updated;
        lock (_gate)
        {
            updated = _current = _current with { TotalBytes = _current.TotalBytes + bytes };
            _dirty = true;
        }

        RaiseChanged(updated);
    }

    /// <param name="force">
    /// false の場合、前回の保存から <see cref="KeepAliveOptions.DataUsageSaveInterval"/> 経っていなければ保存しない
    /// (動作中のファイルの書き込みを減らすため)。
    /// </param>
    private async Task SaveAsync(bool force = true)
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            DataUsage snapshot;
            lock (_gate)
            {
                if (!_loaded || !_dirty || (!force && now - _lastSavedAt < _options.DataUsageSaveInterval))
                    return;

                snapshot = _current;
                _dirty = false;
                _lastSavedAt = now;
            }

            try
            {
                await _store.SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 次の機会に保存し直す。計測とキープアライブは止めない
                lock (_gate)
                    _dirty = true;
                _log.Error($"通信量の累計を保存できませんでした: {ex.Message}");
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void RaiseChanged(DataUsage usage)
    {
        try
        {
            Changed?.Invoke(this, usage);
        }
        catch (Exception ex)
        {
            _log.Error($"通信量の通知先でエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 1 回分の計測。アダプターの送受信量を読み、前回の読み取り値からの増加分を累計に加える。
    /// </summary>
    private sealed class Measurement : IDataUsageMeasurement
    {
        private readonly DataUsageTracker _owner;

        /// <summary>前回の読み取り値 (増加分の基準)。</summary>
        private DataUsageReading? _last;

        private bool _readErrorLogged;
        private bool _disposed;

        public Measurement(DataUsageTracker owner)
        {
            _owner = owner;
            // 開始時点の値を基準にする (開始前の通信は数えない)
            Measure();
        }

        public async Task SampleAsync()
        {
            if (_disposed)
                return;

            _owner.Add(Measure());
            await _owner.SaveAsync(force: false).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;

            _owner.Add(Measure());
            await _owner.SaveAsync().ConfigureAwait(false);
        }

        /// <summary>送受信量を読み、前回からの増加分を返す。</summary>
        private long Measure()
        {
            // ネットワークが無い間 (テザリングの切断中など) は、前回の値を基準のまま残す
            if (Read() is not { } reading)
                return 0;

            var previous = _last;
            _last = reading;
            if (previous is null || previous.InterfaceId != reading.InterfaceId)
            {
                // 別のアダプターの値どうしは比べられないため、新しいアダプターの値を基準にする
                _owner._log.Info($"通信量の計測対象: {reading.InterfaceName}");
                return 0;
            }

            // 値が減った場合は、アダプターの再接続などで OS の数える値が 0 に戻ったとみなす
            return reading.TotalBytes >= previous.TotalBytes
                ? reading.TotalBytes - previous.TotalBytes
                : reading.TotalBytes;
        }

        private DataUsageReading? Read()
        {
            try
            {
                return _owner._meter.Read();
            }
            catch (Exception ex)
            {
                // 計測できなくてもキープアライブは続ける。同じ失敗を繰り返しログに出さない
                if (!_readErrorLogged)
                {
                    _readErrorLogged = true;
                    _owner._log.Warning($"通信量を計測できませんでした: {ex.GetType().Name}: {ex.Message}");
                }

                return null;
            }
        }
    }
}
