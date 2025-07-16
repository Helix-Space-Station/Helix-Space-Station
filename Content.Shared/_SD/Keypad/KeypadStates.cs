using Robust.Shared.Serialization;

namespace Content.Shared.SD.Keypad;

[Serializable, NetSerializable]
public enum KeypadState
{
    Normal,
    AwaitingOldCode,
    AwaitingNewCode
}
