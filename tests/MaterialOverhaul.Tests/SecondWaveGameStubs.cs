using System.Collections.Generic;

namespace UnityEngine { public sealed class GameObject { } }
public interface IGameObjectEffectDescriptor
{
    List<Descriptor> GetDescriptors(UnityEngine.GameObject go);
}
public struct CellOffset
{
    public int x, y;
    public CellOffset(int x, int y) { this.x = x; this.y = y; }
}
public struct HashedString { }
public static class LogicOperationalController
{
    public static readonly HashedString PORT_ID = new HashedString();
}
namespace MaterialOverhaul.Game
{
    public partial class MaterialOverhaulFab
    {
        public float DisplayWorkingCeiling;
        private bool TryGetHeatCeiling(out float value)
        { value = DisplayWorkingCeiling; return value > 0f; }
    }
}
