using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 設定された方式に対応する <see cref="IKeepAliveStrategy"/> を返す。
/// </summary>
public interface IKeepAliveStrategyResolver
{
    IReadOnlyList<IKeepAliveStrategy> Strategies { get; }

    IKeepAliveStrategy Resolve(KeepAliveMethod method);
}
