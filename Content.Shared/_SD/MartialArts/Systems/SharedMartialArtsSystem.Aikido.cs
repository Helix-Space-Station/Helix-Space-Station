using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Audio;

namespace Content.Shared.ADT.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeCosmicAikido()
    {
        SubscribeLocalEvent<CanPerformComboComponent, AikidoOpenPalmComboPerformedEvent>(OnAikidoOpenPalmCombo);
        SubscribeLocalEvent<CanPerformComboComponent, AikidoHighKickComboPerformedEvent>(OnAikidoHighKickCombo);
        SubscribeLocalEvent<CanPerformComboComponent, AikidoLowKickComboPerformedEvent>(OnAikidoLowKickCombo);

        SubscribeLocalEvent<GrantCosmicAikidoComponent, UseInHandEvent>(OnGrantCQCUse);
    }

    private void OnAikidoOpenPalmCombo(Entity<CanPerformComboComponent> ent, ref AikidoOpenPalmComboPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, Loc.GetString("aikido-combo-open-palm"));
        ent.Comp.LastAttacks.Clear();
    }

    private void OnAikidoHighKickCombo(Entity<CanPerformComboComponent> ent, ref AikidoHighKickComboPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(proto.ParalyzeTime), true, true, proto.DropItems);

        var mapPos = _transform.GetMapCoordinates(ent).Position;
        var hitPos = _transform.GetMapCoordinates(target).Position;
        var dir = hitPos - mapPos;

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _grabThrown.Throw(target, ent, dir, proto.ThrownSpeed, behavior: proto.DropItems);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, Loc.GetString("aikido-combo-high-kick"));
        ent.Comp.LastAttacks.Clear();
    }

    private void OnAikidoLowKickCombo(Entity<CanPerformComboComponent> ent, ref AikidoLowKickComboPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(proto.ParalyzeTime), true, true, proto.DropItems);
        if (proto.DropItems)
            _hands.TryDrop(target);
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, Loc.GetString("aikido-combo-low-kick"));
        ent.Comp.LastAttacks.Clear();
    }
}
