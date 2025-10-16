using Content.Server.SD.MachineSafety.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.SD.MachineSafety.Systems;

public sealed class MachineSafetySystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private ISawmill _sawmill = default!;
    private const float VacuumThreshold = 5f; // 5 kPa - минимальное рабочее давление

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("machine.safety");

        SubscribeLocalEvent<MachineSafetyComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MachineSafetyComponent, ApcPowerReceiverComponent, TransformComponent>();
        var currentTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var safety, out var power, out var xform))
        {
            if (!xform.Anchored || xform.MapUid == null)
                continue;

            CheckAtmosphere(uid, safety, power, xform);

            if (safety.HasAtmosphere && !power.PowerDisabled)
            {
                CheckOverheat(uid, safety, xform, currentTime);
            }
        }
    }

    private void OnMapInit(EntityUid uid, MachineSafetyComponent component, MapInitEvent args)
    {
        // Конвертируем секунды в тики (1 секунда = 60 тиков)
        component.MaxOverheatTimeTicks = component.MaxOverheatTimeSeconds * 60;
        component.OverheatTimer = TimeSpan.Zero;
        component.LastAlertTime = TimeSpan.Zero;
        component.HasAtmosphere = true;
        _sawmill.Debug($"Machine safety system initialized for {uid}. Max overheat time: {component.MaxOverheatTimeSeconds}s ({component.MaxOverheatTimeTicks} ticks)");
    }

    private void CheckAtmosphere(EntityUid uid, MachineSafetyComponent safety, ApcPowerReceiverComponent power, TransformComponent xform)
    {
        try
        {
            var mixture = _atmosphere.GetContainingMixture(uid, ignoreExposed: false, excite: true);

            if (mixture == null)
            {
                _sawmill.Warning($"Machine {uid}: GetContainingMixture returned null");
                SetAtmosphereState(uid, safety, power, false);
                return;
            }

            ProcessAtmosphereData(uid, safety, power, mixture);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error checking atmosphere for {uid}: {ex.Message}");
            SetAtmosphereState(uid, safety, power, true);
        }
    }

    private void ProcessAtmosphereData(EntityUid uid, MachineSafetyComponent safety, ApcPowerReceiverComponent power, GasMixture mixture)
    {
        var pressureKpa = mixture.Pressure;
        var temperature = mixture.Temperature;
        var totalMoles = mixture.TotalMoles;

        _sawmill.Debug($"=== MACHINE {uid} ATMOS DATA ===");
        _sawmill.Debug($"Pressure: {pressureKpa:F1} kPa");
        _sawmill.Debug($"Temperature: {temperature} K ({temperature - 273.15:F1}°C)");
        _sawmill.Debug($"Total moles: {totalMoles:F3}");
        _sawmill.Debug($"Volume: {mixture.Volume}");

        var hasAtmosphere = pressureKpa >= VacuumThreshold && totalMoles > 0.01f;

        if (pressureKpa < VacuumThreshold)
        {
            _sawmill.Debug($"Low pressure: {pressureKpa:F1} kPa < {VacuumThreshold} kPa threshold");
        }
        if (totalMoles <= 0.01f)
        {
            _sawmill.Debug($"Low gas content: {totalMoles:F3} moles");
        }

        _sawmill.Debug($"Has atmosphere: {hasAtmosphere}");
        _sawmill.Debug($"=== END ATMOS DATA ===");

        SetAtmosphereState(uid, safety, power, hasAtmosphere);
    }

    private void SetAtmosphereState(EntityUid uid, MachineSafetyComponent safety, ApcPowerReceiverComponent power, bool hasAtmosphere)
    {
        var wasAtmosphere = safety.HasAtmosphere;
        safety.HasAtmosphere = hasAtmosphere;

        var machineName = Name(uid);

        if (hasAtmosphere)
        {
            if (power.PowerDisabled)
            {
                _sawmill.Info($"Machine {uid}: POWER ON - atmosphere restored");
                SetPower(uid, power, false);

                if (!wasAtmosphere)
                {
                    SendRadioAlert(uid, safety, "machine-safety-atmosphere-restored");
                    ResetWarningFlags(safety);
                }
            }
        }
        else
        {
            if (!power.PowerDisabled)
            {
                _sawmill.Warning($"Machine {uid}: POWER OFF - vacuum detected");
                SetPower(uid, power, true);

                // ОСТАНОВКА ПЕРЕГРЕВА при отключении питания
                if (safety.IsOverheating)
                {
                    _sawmill.Info($"Machine {uid}: Overheat stopped due to power loss");
                    safety.IsOverheating = false;
                    safety.OverheatTimer = TimeSpan.Zero;
                }

                if (wasAtmosphere)
                {
                    SendRadioAlert(uid, safety, "machine-safety-vacuum-shutdown");
                }
            }
        }
    }

    private void SetPower(EntityUid uid, ApcPowerReceiverComponent power, bool disabled)
    {
        power.PowerDisabled = disabled;
        Dirty(uid, power);
    }

    private void CheckOverheat(EntityUid uid, MachineSafetyComponent safety, TransformComponent xform, TimeSpan currentTime)
    {
        var mixture = _atmosphere.GetContainingMixture(uid, ignoreExposed: false, excite: false);
        if (mixture == null)
            return;

        var temperature = mixture.Temperature;
        var machineName = Name(uid);

        _sawmill.Debug($"Machine {uid} overheat check: Temp={temperature:F1}K, OverheatTimer={safety.OverheatTimer.TotalSeconds:F0}s");

        // МГНОВЕННЫЙ ВЗРЫВ при критической температуре
        if (temperature > safety.CriticalTemperature)
        {
            _sawmill.Error($"Machine {uid}: CRITICAL OVERHEAT - {temperature:F1}K");
            SendRadioAlert(uid, safety, "machine-safety-meltdown");
            TriggerMeltdown(uid);
            return;
        }

        // ПРОВЕРКА ПЕРЕГРЕВА - только по температуре
        if (temperature > 320f)
        {
            if (!safety.IsOverheating)
            {
                safety.IsOverheating = true;
                safety.OverheatTimer = TimeSpan.Zero;
                _sawmill.Warning($"Machine {uid}: Overheating started - {temperature:F1}K");
                SendRadioAlert(uid, safety, "machine-safety-overheat-warning");
            }

            safety.OverheatTimer += TimeSpan.FromSeconds(1);

            var timeLeftTicks = safety.MaxOverheatTimeTicks - safety.OverheatTimer.TotalSeconds;
            var timeLeftSeconds = timeLeftTicks;

            // ПРЕДУПРЕЖДЕНИЯ ПО ВРЕМЕНИ
            if (timeLeftSeconds <= 300 && !safety.Warned5Min) // 5 минут
            {
                _sawmill.Warning($"Machine {uid}: 5 minutes until meltdown");
                SendRadioAlert(uid, safety, "machine-safety-meltdown-5min");
                safety.Warned5Min = true;
            }
            else if (timeLeftSeconds <= 180 && !safety.Warned3Min) // 3 минуты
            {
                _sawmill.Warning($"Machine {uid}: 3 minutes until meltdown");
                SendRadioAlert(uid, safety, "machine-safety-meltdown-3min");
                safety.Warned3Min = true;
            }
            else if (timeLeftSeconds <= 60 && !safety.Warned1Min) // 1 минута
            {
                _sawmill.Warning($"Machine {uid}: 1 minute until meltdown");
                SendRadioAlert(uid, safety, "machine-safety-meltdown-1min");
                safety.Warned1Min = true;
            }
            else if (timeLeftSeconds <= 30 && !safety.Warned30Sec) // 30 секунд
            {
                _sawmill.Warning($"Machine {uid}: 30 seconds until meltdown");
                SendRadioAlert(uid, safety, "machine-safety-meltdown-30sec");
                safety.Warned30Sec = true;
            }
            else if (timeLeftSeconds <= 10 && !safety.Warned10Sec) // 10 секунд
            {
                _sawmill.Warning($"Machine {uid}: 10 seconds until meltdown");
                SendRadioAlert(uid, safety, "machine-safety-meltdown-10sec");
                safety.Warned10Sec = true;
            }

            // ВЗРЫВ при превышении времени
            if (safety.OverheatTimer.TotalSeconds > safety.MaxOverheatTimeTicks)
            {
                _sawmill.Error($"Machine {uid}: MELTDOWN - overheating for {safety.OverheatTimer.TotalSeconds:F0}s");
                SendRadioAlert(uid, safety, "machine-safety-meltdown");
                TriggerMeltdown(uid);
            }
        }
        else
        {
            // ОХЛАЖДЕНИЕ
            if (safety.IsOverheating)
            {
                _sawmill.Info($"Machine {uid}: Cooling restored");
                SendRadioAlert(uid, safety, "machine-safety-cooling-restored");
                safety.IsOverheating = false;
                safety.OverheatTimer = TimeSpan.Zero;
                ResetWarningFlags(safety);
            }
        }
    }

    private void ResetWarningFlags(MachineSafetyComponent safety)
    {
        safety.Warned5Min = false;
        safety.Warned3Min = false;
        safety.Warned1Min = false;
        safety.Warned30Sec = false;
        safety.Warned10Sec = false;
        safety.HasSentCriticalAlert = false;
    }

    private void SendRadioAlert(EntityUid uid, MachineSafetyComponent safety, string messageKey)
    {
        var currentTime = _gameTiming.CurTime;
        if (currentTime - safety.LastAlertTime < TimeSpan.FromSeconds(10))
            return;

        safety.LastAlertTime = currentTime;

        var machineName = Name(uid);
        var message = Loc.GetString(messageKey, ("machine", (object)machineName));
        _radio.SendRadioMessage(uid, message, safety.AlertChannel, uid);
        _sawmill.Info($"Radio: {message}");
    }

    private void TriggerMeltdown(EntityUid uid)
    {
        _sawmill.Error($"Machine {uid}: MELTDOWN TRIGGERED");

        if (TryComp<MachineSafetyComponent>(uid, out var safety))
        {
            var machineName = Name(uid);
            SendRadioAlert(uid, safety, "machine-safety-meltdown");
        }

        var coordinates = Transform(uid).Coordinates;
        var mapCoordinates = _transformSystem.ToMapCoordinates(coordinates);

        _explosion.QueueExplosion(
            mapCoordinates,
            "Default",
            totalIntensity: 10f,
            slope: 2f,
            maxTileIntensity: 4f,
            cause: uid,
            tileBreakScale: 1f,
            maxTileBreak: 10,
            canCreateVacuum: true,
            addLog: true);

        if (!Deleted(uid) && !Terminating(uid))
        {
            QueueDel(uid);
        }
    }
}
