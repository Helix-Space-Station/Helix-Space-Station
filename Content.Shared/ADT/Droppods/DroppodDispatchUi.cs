using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.ADT.Droppods;

[Serializable, NetSerializable]
public enum DroppodDispatchUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DroppodDispatchCargoInfo
{
    public NetEntity Uid { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsMob { get; init; }
}

[Serializable, NetSerializable]
public sealed class DroppodDispatchGhostOption
{
    public EntProtoId Prototype { get; init; }
    public string Name { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DroppodDispatchBeaconInfo
{
    public NetEntity Uid { get; init; }
    public string Name { get; init; } = string.Empty;
    public Vector2 WorldPos { get; init; }
}

[Serializable, NetSerializable]
public sealed class DroppodDispatchConsoleBuiState : BoundUserInterfaceState
{
    public List<DroppodDispatchCargoInfo> Cargo { get; init; } = new();
    public List<DroppodDispatchGhostOption> GhostOptions { get; init; } = new();
    public List<DroppodDispatchBeaconInfo> Beacons { get; init; } = new();
    public bool CanLaunch { get; init; }
    public bool Powered { get; init; }
    public int CooldownRemaining { get; init; }
    public int MaxCargo { get; init; }
}

[Serializable, NetSerializable]
public sealed class DroppodDispatchLaunchMessage : BoundUserInterfaceMessage
{
    public NetEntity TargetBeacon { get; init; }
    public List<NetEntity> Cargo { get; init; } = new();
    public List<EntProtoId> ExtraSpawns { get; init; } = new();
}
