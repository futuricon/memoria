namespace Memoria.Shared.Kernel.Results;

/// <summary>
/// Пустое значение для команд, не возвращающих payload.
/// Используется как <c>Result&lt;Unit&gt;</c> в MediatR-командах.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value;

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}