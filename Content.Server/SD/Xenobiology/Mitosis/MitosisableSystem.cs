using System.Numerics;
using Content.Shared.Polymorph.Systems;
using Content.Server.SD.Dissolvement;
using Content.Server.Polymorph.Systems;
using Content.Server.Polymorph.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Jittering;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Server.SD.Mitosis;

public sealed class MitosisableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly DissolvementOnAttackSystem _dissolvementSystem = default!;
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _scheduledMitosis = new();

    public override void Update(float frameTime)
    {
        var toProcess = new List<(EntityUid uid, MitosisableComponent comp)>();

        var xenoQuery = EntityQueryEnumerator<MitosisableComponent, TransformComponent>();
        while (xenoQuery.MoveNext(out var uid, out var component, out var transform))
        {
            if (!TryComp<HungerComponent>(uid, out var hunger))
                return;

            if (_hunger.GetHunger(hunger) >= component.HungerThreshold)
            {
                var now = _timing.CurTime;
                if (!_scheduledMitosis.ContainsKey(uid))
                {
                    _scheduledMitosis[uid] = now + TimeSpan.FromSeconds(9);
                }

                _jittering.DoJitter(uid, TimeSpan.FromSeconds(5), true, 10f, 4f, true);
            }

            if (_scheduledMitosis.TryGetValue(uid, out var scheduled) && _timing.CurTime >= scheduled)
            {
                _scheduledMitosis.Remove(uid);
                toProcess.Add((uid, component));
            }
        }

        // Обрабатываем митоз вне перечисления, чтобы избежать фатал ерроров
        foreach (var (uid, comp) in toProcess)
        {
            if (!EntityManager.EntityExists(uid) || !TryComp<MitosisableComponent>(uid, out var component))
                return;

            Mitos(uid, component);
        }
    }

    private void Mitos(EntityUid uid, MitosisableComponent component)
    {
        component.IsMitosising = true;

        if (TryComp<DissolvementOnAttackComponent>(uid, out var dissolvComp))
        {
            _dissolvementSystem.StopDissolvement(uid);
        }

        var prototype = MetaData(uid).EntityPrototype?.ID;

        var coords = Transform(uid).Coordinates;

        for (int i = 0; i < component.BreedCount; i++)
        {
            string? toSpawn = null;

            if (_robustRandom.Prob(component.Mutationchance))
            {
                if (component.Mutagen != null && component.Mutagen.Count > 0)
                {
                    toSpawn = component.Mutagen[_robustRandom.Next(component.Mutagen.Count)];
                }
            }
            
            else
            {
                toSpawn = prototype;
            }

            if (string.IsNullOrEmpty(toSpawn))
                continue;

            Spawn(toSpawn, coords);
        }

        // Удаляет исходное существо
        if (EntityManager.EntityExists(uid))
            EntityManager.DeleteEntity(uid);
    }
}