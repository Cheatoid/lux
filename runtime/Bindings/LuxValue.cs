namespace Lux.Runtime.Bindings;

/// <summary>
/// Base class for binding values that know how to push themselves onto a Lua state.
/// Concrete implementations are <see cref="LuxTable"/> and <see cref="LuxClass"/>.
/// </summary>
public abstract class LuxValue
{
    /// <summary>
    /// Pushes this value onto the runtime's Lua stack. The implementation must leave
    /// exactly one value on the stack.
    /// </summary>
    internal abstract void Push(LuxRuntime rt);
}
