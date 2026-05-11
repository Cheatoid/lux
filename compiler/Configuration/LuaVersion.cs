using Lux.Configuration.Converter;
using Tomlyn.Serialization;

namespace Lux.Configuration;

/// <summary>
/// The supported Lua versions Lux can transpile to.
/// </summary>
[TomlConverter(typeof(LuaVersionConverter))]
public enum LuaVersion
{
    Lua51,
    Lua52,
    Lua53,
    Lua54,
    LuaJIT
}