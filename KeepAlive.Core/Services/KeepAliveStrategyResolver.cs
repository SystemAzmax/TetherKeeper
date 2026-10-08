using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

public sealed class KeepAliveStrategyResolver : IKeepAliveStrategyResolver
{
    private readonly Dictionary<KeepAliveMethod, IKeepAliveStrategy> _byMethod;

    public KeepAliveStrategyResolver(IEnumerable<IKeepAliveStrategy> strategies)
    {
        Strategies = strategies.ToArray();
        _byMethod = Strategies.ToDictionary(s => s.Method);
    }

    public IReadOnlyList<IKeepAliveStrategy> Strategies { get; }

    public IKeepAliveStrategy Resolve(KeepAliveMethod method) =>
        _byMethod.TryGetValue(method, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"通信方式 {method} の Strategy が登録されていません。");
}
