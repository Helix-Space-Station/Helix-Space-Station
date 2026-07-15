using Robust.Shared.Serialization;

namespace Content.Shared.ADT.MartialArts;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AikidoOpenPalmComboPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AikidoHighKickComboPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AikidoLowKickComboPerformedEvent : EntityEventArgs;
