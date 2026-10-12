using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Optional integration with the audited 0.0.2 build. The foreign handler keeps
    // ownership of stance intent, curves and springs; we only add a temporary suppressor.
    public sealed class StanceOverhaulCompatibility : IDisposable
    {
        private const string StanceGuid = "com.fontaine.spt-stance-overhaul";
        private const string CommonGuid = "com.fontaine.spt-realism-common-lib";
        private const string Owner = "com.tylevo.fieldattachments.stance-compatibility";
        private const string StanceMvid = "0cc4693e-a0dc-43da-8570-ba6d4518c934";
        private const string CommonMvid = "a5315d91-412f-4d91-a487-577c2bb42d6a";
        private const float NeutralTransitionSeconds = .48f;
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static StanceOverhaulCompatibility? _owner;
        private readonly ReadAccess _read;
        private readonly Action<string> _log;
        private Harmony? _harmony;
        private FieldInfo? _controllerInstance, _inputHandler, _stanceState, _playerState;
        private FieldInfo? _primarySlot, _incomingSlot, _chosen;
        private FieldInfo? _springCurrent, _springZero, _springVelocity;
        private PropertyInfo? _awakeRan, _commonPlayer;
        private PropertyInfo? _defaultStance, _stanceType, _slotStance, _slotProgress, _slotDirection, _slotCurve, _slotTransition;
        private PropertyInfo? _transitionFrom, _transitionTo, _statsHandler, _globalSpeed, _blendSpeed;
        private PropertyInfo? _stancePosition, _stanceRotation, _positionSpring, _rotationSpring;
        private MethodInfo? _cancelAll, _reconcile;
        private MethodInfo? _transitionFromModifier, _transitionToModifier, _statsSpeed;
        private object? _noneStance;
        private object? _controller, _handler, _state, _player, _hands, _weapon;
        private bool _initialized, _ready, _active, _suppress, _disposed, _failureLogged;
        private int _settledFrame = -1, _settledSamples;

        public StanceAvailability Availability
        {
            get
            {
                if (!Chainloader.PluginInfos.ContainsKey(StanceGuid)) return StanceAvailability.NotDetected;
                if (!Chainloader.PluginInfos.TryGetValue(CommonGuid, out var common) || common.Instance == null)
                    return StanceAvailability.MissingCommonLibrary;
                return _ready && !_disposed ? StanceAvailability.Ready : StanceAvailability.Unavailable;
            }
        }
        public bool Active => _active;
        public float EntrySeconds { get; private set; } = .48f;
        public float ReturnSeconds { get; private set; } = .766666f;
        public string Status { get; private set; } = "Stance compatibility not initialized.";

        public StanceOverhaulCompatibility(ReadAccess read, Action<string> log)
        { _read = read; _log = log; }

        public void Initialize()
        {
            if (_initialized || _disposed) return;
            _initialized = true;
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(StanceGuid, out var stancePlugin))
                { Status = "Stance Overhaul not installed; normal attachment animation."; return; }
                if (!Chainloader.PluginInfos.TryGetValue(CommonGuid, out var commonPlugin) ||
                    stancePlugin.Instance == null || commonPlugin.Instance == null ||
                    stancePlugin.Metadata.Version.ToString() != "0.0.2" ||
                    commonPlugin.Metadata.Version.ToString() != "0.0.2")
                    throw new InvalidOperationException("requires the audited Stance Overhaul and Realism Common Library 0.0.2 builds");
                Assembly stance = stancePlugin.Instance.GetType().Assembly;
                Assembly common = commonPlugin.Instance.GetType().Assembly;
                if (stance.ManifestModule.ModuleVersionId.ToString() != StanceMvid ||
                    common.ManifestModule.ModuleVersionId.ToString() != CommonMvid)
                    throw new InvalidOperationException("installed stance/common library module identity is not audited");
                if (_owner != null && !ReferenceEquals(_owner, this))
                    throw new InvalidOperationException("another stance adapter already owns the hook");

                Type plugin = stance.GetType("StanceOverhaul.Plugin", true)!;
                Type controller = stance.GetType("StanceOverhaul.Controllers.StanceController", true)!;
                Type handler = stance.GetType("StanceOverhaul.SubSystem.StanceInput.StanceInputHandler", true)!;
                Type state = stance.GetType("StanceOverhaul.State.StanceState", true)!;
                Type stanceInterface = stance.GetType("StanceOverhaul.Stances.IStance", true)!;
                Type commonType = common.GetType("RealismCommonLib.Plugin", true)!;
                Type playerState = common.GetType("RealismCommonLib.StateControllers.InstanceState.PlayerState", true)!;
                if (!typeof(MonoBehaviour).IsAssignableFrom(controller))
                    throw new InvalidOperationException("stance controller type changed");
                _controllerInstance = RequireField(plugin, "StanceControllerInstance", controller, true, true);
                _inputHandler = RequireField(controller, "_inputHandler", handler, false, false);
                _stanceState = RequireField(controller, "_stanceState", state, false, false);
                _awakeRan = RequireProperty(controller, "AwakeRan", typeof(bool), false);
                _playerState = RequireField(commonType, "PlayerStateInstance", playerState, true, true);
                _commonPlayer = RequireProperty(playerState, "Player", typeof(EFT.Player), false);
                _cancelAll = RequireMethod(state, "CancelAll", typeof(void), true, Type.EmptyTypes);
                _reconcile = RequireMethod(handler, "Reconcile", typeof(void), false, Type.EmptyTypes);
                MethodInfo suppress = RequireMethod(handler, "IsSuppressed", typeof(bool), false, new[] { stanceInterface });
                BindTiming(stance, controller, handler, state, stanceInterface);
                _harmony = new Harmony(Owner);
                _owner = this;
                _harmony.Patch(suppress, postfix: new HarmonyMethod(typeof(StanceOverhaulCompatibility), nameof(AfterIsSuppressed))
                    { priority = Priority.Last });
                _ready = true;
                Status = "Stance Overhaul 0.0.2 compatibility ready.";
                Log(Status);
            }
            catch (Exception e) { Disable(e); }
        }

        public bool Begin(RaidSnapshot snapshot)
        {
            Initialize();
            if (!_ready || _disposed) return false;
            try
            {
                if (_active && _suppress && ReferenceEquals(snapshot.Player, _player) && ReferenceEquals(snapshot.Weapon, _weapon) && ContextCurrent())
                { ReadTimingProfile(); ResetSettling(); return true; }
                End();
                if (!_ready) return false;
                object? controller = _controllerInstance!.GetValue(null);
                if (!(controller is MonoBehaviour behaviour) || behaviour == null || !behaviour.isActiveAndEnabled)
                    return false;
                object? handler = _inputHandler!.GetValue(controller);
                object? state = _stanceState!.GetValue(controller);
                object? hands = _read.Get(snapshot.Player, "HandsController");
                if (!ContextCurrent(controller, handler, state, snapshot.Player, hands, snapshot.Weapon)) return false;
                _controller = controller; _handler = handler; _state = state;
                _player = snapshot.Player; _hands = hands; _weapon = snapshot.Weapon;
                _active = _suppress = true;
                // CancelAll also discards an incoming stance paused behind an exiting one.
                // Reconcile alone can miss it when ActiveStance is temporarily null.
                _cancelAll!.Invoke(state, null);
                ReadTimingProfile();
                ResetSettling();
                Status = "Stance yielded to attachment animation.";
                return true;
            }
            catch (Exception e) { End(); Disable(e); return false; }
        }

        public void Tick(bool keepActive)
        {
            if (!_active) return;
            try { if (!keepActive || !ContextCurrent()) End(); }
            catch (Exception e) { ClearContext(); Disable(e); }
        }

        public bool EntrySettled
        {
            get
            {
                if (!_active || !_suppress) return true;
                try
                {
                    if (!ContextCurrent()) { End(); return true; }
                    bool settled = _primarySlot!.GetValue(_state) == null && _incomingSlot!.GetValue(_state) == null &&
                        NearZero(ReadVector(_stancePosition!.GetValue(_controller, null)), .002f) &&
                        NearZero(ReadVector(_stanceRotation!.GetValue(_controller, null)), .15f) &&
                        SpringSettled(_positionSpring!.GetValue(_controller, null), .002f, .0005f) &&
                        SpringSettled(_rotationSpring!.GetValue(_controller, null), .15f, .03f);
                    if (!settled) { ResetSettling(); return false; }
                    int frame = Time.frameCount;
                    if (frame != _settledFrame)
                    { _settledFrame = frame; _settledSamples = Math.Min(2, _settledSamples + 1); }
                    return _settledSamples >= 2;
                }
                catch (Exception e) { End(); Disable(e); return true; }
            }
        }

        // Normal menu close can blend our authored return with the foreign stance's
        // own re-entry. Inventory-action returns keep suppression until their sequence ends.
        public bool BeginReturn()
        {
            if (!_active || _disposed) return false;
            try
            {
                if (!ContextCurrent()) { End(); return false; }
                if (!_suppress) return true;
                ReturnSeconds = ReturnDuration(ChosenOrDefault(), SmoothingAllowance());
                _suppress = false;
                ResetSettling();
                _reconcile!.Invoke(_handler, null);
                Status = "Stance restoring alongside attachment return.";
                Log("STANCE_RETURN seconds=" + ReturnSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e) { End(); Disable(e); return false; }
        }

        public void End()
        {
            if (!_active) { ClearContext(); return; }
            object? controller = _controller, handler = _handler, state = _state;
            object? player = _player, hands = _hands, weapon = _weapon;
            bool restore = _suppress;
            // Foreign callbacks must see suppression released, even if reconciliation throws.
            ClearContext();
            try
            {
                if (restore && ContextCurrent(controller, handler, state, player, hands, weapon))
                    _reconcile!.Invoke(handler, null);
                if (_ready) Status = "Stance Overhaul 0.0.2 compatibility ready.";
            }
            catch (Exception e) { Disable(e); }
        }

        private void BindTiming(Assembly assembly, Type controller, Type handler, Type state, Type stanceInterface)
        {
            Type slot = assembly.GetType("StanceOverhaul.State.StanceSlot", true)!;
            Type transition = assembly.GetType("StanceOverhaul.State.StanceTransitionContext", true)!;
            Type stanceType = assembly.GetType("StanceOverhaul.Enums.EStanceType", true)!;
            Type curveType = assembly.GetType("StanceOverhaul.Enums.ECurveType", true)!;
            Type stats = assembly.GetType("StanceOverhaul.SubSystem.StatsSystem", true)!;
            Type config = assembly.GetType("StanceOverhaul.PluginConfig", true)!;
            Type optionalStance = typeof(Nullable<>).MakeGenericType(stanceType);
            Type spring = typeof(EFT.Animations.Spring);
            _noneStance = Enum.Parse(stanceType, "None");
            _primarySlot = RequireField(state, "_primarySlot", slot, false, false);
            _incomingSlot = RequireField(state, "_incoming", slot, false, false);
            _chosen = RequireField(handler, "_chosen", stanceInterface, false, false);
            _defaultStance = RequireProperty(handler, "DefaultStance", stanceInterface, true, false);
            _stanceType = RequireProperty(stanceInterface, "StanceType", stanceType, false);
            _slotStance = RequireProperty(slot, "Stance", stanceInterface, false);
            _slotProgress = RequireProperty(slot, "Progress", typeof(float), false);
            _slotDirection = RequireProperty(slot, "Direction", typeof(int), false);
            _slotCurve = RequireProperty(slot, "ActiveCurveType", curveType, false);
            _slotTransition = RequireProperty(slot, "Transition", transition, false);
            _transitionFrom = RequireProperty(transition, "From", stanceType, false);
            _transitionTo = RequireProperty(transition, "To", stanceType, false);
            _transitionFromModifier = RequireMethod(stanceInterface, "TransitionFromModifier", typeof(float), true, new[] { optionalStance });
            _transitionToModifier = RequireMethod(stanceInterface, "TransitionToSpeedModifier", typeof(float), true, new[] { optionalStance });
            _statsHandler = RequireProperty(controller, "StatsHandlerInstance", stats, false);
            _statsSpeed = RequireMethod(stats, "GetStanceSpeedModifier", typeof(float), true, new[] { typeof(float) });
            _globalSpeed = RequireProperty(config, "GlobalStanceSpeed", typeof(ConfigEntry<float>), true);
            _blendSpeed = RequireProperty(config, "StanceBlendSpeed", typeof(ConfigEntry<float>), true);
            _stancePosition = RequireProperty(controller, "StancePosition", typeof(Vector3), false);
            _stanceRotation = RequireProperty(controller, "StanceRotation", typeof(Vector3), false);
            _positionSpring = RequireProperty(controller, "StancePositionSpring", spring, false);
            _rotationSpring = RequireProperty(controller, "StanceRotationSpring", spring, false);
            _springCurrent = RequireField(spring, "Current", typeof(Vector3), false, true);
            _springZero = RequireField(spring, "Zero", typeof(Vector3), false, true);
            _springVelocity = RequireField(spring, "Velocity", typeof(Vector3), false, true);
        }

        private void ReadTimingProfile()
        {
            object? target = ChosenOrDefault();
            float allowance = SmoothingAllowance();
            object? slot = _primarySlot!.GetValue(_state);
            float exit = slot != null ? RemainingExit(slot) : target != null ?
                1f / TransitionSpeed(target, _stanceType!.GetValue(target, null), _noneStance) : 0;
            // No chosen/default stance and no remaining slot means there is no foreign
            // curve to time. Keep the smooth blend without inventing a slow neutral exit.
            EntrySeconds = slot == null && target == null ? NeutralTransitionSeconds : Duration(exit, allowance);
            ReturnSeconds = ReturnDuration(target, allowance);
            Log("STANCE_TIMING entry=" + EntrySeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " return=" + ReturnSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " curveRemaining=" + exit.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }

        private object? ChosenOrDefault() => _chosen!.GetValue(_handler) ?? _defaultStance!.GetValue(null, null);

        private float ReturnDuration(object? target, float allowance) => target == null ? NeutralTransitionSeconds :
            Duration(1f / TransitionSpeed(target, _noneStance, _stanceType!.GetValue(target, null)), allowance);

        private float RemainingExit(object slot)
        {
            float progress = ReadFloat(_slotProgress!.GetValue(slot, null));
            if (progress < 0 || progress > 1) throw new InvalidOperationException("stance progress outside its curve");
            int direction = (int)_slotDirection!.GetValue(slot, null)!;
            string curve = _slotCurve!.GetValue(slot, null)!.ToString()!;
            float remaining;
            if (curve == "Exit" && direction >= 0) remaining = 1 - progress;
            else if (curve == "Enter" && direction <= 0) remaining = progress;
            else throw new InvalidOperationException("stance is not returning toward idle");
            object stance = _slotStance!.GetValue(slot, null)!;
            object transition = _slotTransition!.GetValue(slot, null)!;
            return remaining / TransitionSpeed(stance, _transitionFrom!.GetValue(transition, null),
                _transitionTo!.GetValue(transition, null));
        }

        private float TransitionSpeed(object? stance, object? from, object? to)
        {
            float speed = ReadConfig(_globalSpeed!);
            if (stance != null)
                speed *= ReadFloat(_transitionFromModifier!.Invoke(stance, new[] { from })) *
                    ReadFloat(_transitionToModifier!.Invoke(stance, new[] { to }));
            if (!Finite(speed) || speed < 0) throw new InvalidOperationException("invalid stance speed");
            object stats = _statsHandler!.GetValue(_controller, null)!;
            float modified = ReadFloat(_statsSpeed!.Invoke(stats, new object[] { speed }));
            // Match StanceSlot's exact clamp, including a configured maximum below one.
            float result = Mathf.Clamp(modified, 1f, speed);
            if (!Finite(result) || result <= 0) throw new InvalidOperationException("invalid effective stance speed");
            return result;
        }

        private float SmoothingAllowance()
        {
            float blend = ReadConfig(_blendSpeed!);
            if (blend <= 0) throw new InvalidOperationException("invalid stance blend speed");
            return Mathf.Clamp(3f / blend, .1f, .4f);
        }

        private static float Duration(float curveSeconds, float allowance)
        {
            if (!Finite(curveSeconds) || curveSeconds < 0) throw new InvalidOperationException("invalid stance duration");
            return Mathf.Clamp(curveSeconds + allowance, .48f, 2.5f);
        }

        private static float ReadConfig(PropertyInfo property)
        {
            if (!(property.GetValue(null, null) is ConfigEntry<float> entry))
                throw new InvalidOperationException("stance speed setting unavailable");
            return ReadFloat(entry.Value);
        }

        private static float ReadFloat(object? value)
        {
            if (!(value is float number) || !Finite(number)) throw new InvalidOperationException("invalid stance timing value");
            return number;
        }

        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);

        private static Vector3 ReadVector(object? value)
        {
            if (!(value is Vector3 vector) || !Finite(vector.x) || !Finite(vector.y) || !Finite(vector.z))
                throw new InvalidOperationException("invalid stance spring value");
            return vector;
        }

        private bool SpringSettled(object? spring, float positionTolerance, float velocityTolerance)
        {
            if (spring == null) throw new InvalidOperationException("stance spring unavailable");
            return NearZero(ReadVector(_springCurrent!.GetValue(spring)) - ReadVector(_springZero!.GetValue(spring)), positionTolerance) &&
                NearZero(ReadVector(_springVelocity!.GetValue(spring)), velocityTolerance);
        }

        private static bool NearZero(Vector3 vector, float tolerance) => vector.sqrMagnitude <= tolerance * tolerance;
        private void ResetSettling() { _settledFrame = -1; _settledSamples = 0; }

        private bool ContextCurrent() => ContextCurrent(_controller, _handler, _state, _player, _hands, _weapon);

        private bool ContextCurrent(object? controller, object? handler, object? state, object? player, object? hands, object? weapon)
        {
            if (!(controller is MonoBehaviour behaviour) || behaviour == null || !behaviour.isActiveAndEnabled ||
                !(player is Component playerComponent) || playerComponent == null ||
                handler == null || state == null || hands == null || weapon == null ||
                behaviour.gameObject != playerComponent.gameObject ||
                !ReferenceEquals(_controllerInstance!.GetValue(null), controller) ||
                !ReferenceEquals(_inputHandler!.GetValue(controller), handler) ||
                !ReferenceEquals(_stanceState!.GetValue(controller), state) ||
                !Equals(_awakeRan!.GetValue(controller, null), true)) return false;
            object? commonState = _playerState!.GetValue(null);
            return commonState != null && ReferenceEquals(_commonPlayer!.GetValue(commonState, null), player) &&
                ReferenceEquals(_read.Get(player, "HandsController"), hands) &&
                ReferenceEquals(_read.Get(hands, "Item"), weapon) &&
                ReadAccess.Bool(_read.Get(player, "IsYourPlayer")) == true &&
                ReadAccess.Bool(_read.Get(_read.Get(player, "HealthController"), "IsAlive")) == true;
        }

        private static void AfterIsSuppressed(object __instance, ref bool __result)
        {
            StanceOverhaulCompatibility? owner = _owner;
            if (owner == null || !owner._active || !owner._suppress || !ReferenceEquals(owner._handler, __instance)) return;
            try
            {
                if (owner.ContextCurrent()) __result = true;
                else owner.ClearContext(); // Never restore an old weapon's intent onto a new context.
            }
            catch (Exception e) { owner.ClearContext(); owner.Disable(e); }
        }

        private void ClearContext()
        {
            _active = _suppress = false;
            ResetSettling();
            _controller = _handler = _state = _player = _hands = _weapon = null;
        }

        private void Disable(Exception exception)
        {
            ClearContext(); _ready = false;
            Unpatch();
            Status = "Stance compatibility unavailable: " + exception.GetBaseException().Message + "; normal attachment animation retained.";
            if (!_failureLogged) { _failureLogged = true; Log(Status); }
        }

        private void Unpatch()
        {
            if (ReferenceEquals(_owner, this)) _owner = null;
            try { _harmony?.UnpatchSelf(); }
            catch (Exception e) { Log("Stance compatibility hook cleanup: " + e.GetBaseException().Message); }
            _harmony = null;
        }

        private void Log(string message) { try { _log(message); } catch { } }

        private static FieldInfo RequireField(Type type, string name, Type fieldType, bool isStatic, bool isPublic)
        {
            FieldInfo? field = type.GetField(name, InstanceFlags | BindingFlags.Static);
            if (field == null || field.FieldType != fieldType || field.IsStatic != isStatic || field.IsPublic != isPublic)
                throw new InvalidOperationException("stance field changed: " + type.FullName + "." + name);
            return field;
        }

        private static PropertyInfo RequireProperty(Type type, string name, Type propertyType, bool isStatic, bool isPublic = true)
        {
            PropertyInfo? property = type.GetProperty(name, InstanceFlags | BindingFlags.Static);
            MethodInfo? getter = property?.GetGetMethod(!isPublic);
            if (property == null || property.PropertyType != propertyType || property.GetIndexParameters().Length != 0 ||
                getter == null || getter.IsStatic != isStatic || getter.IsPublic != isPublic)
                throw new InvalidOperationException("stance property changed: " + type.FullName + "." + name);
            return property;
        }

        private static MethodInfo RequireMethod(Type type, string name, Type result, bool isPublic, Type[] parameters)
        {
            MethodInfo? method = type.GetMethod(name, InstanceFlags, null, parameters, null);
            if (method == null || method.ReturnType != result || method.IsStatic || method.IsPublic != isPublic || method.ContainsGenericParameters)
                throw new InvalidOperationException("stance method changed: " + type.FullName + "." + name);
            return method;
        }

        public void Dispose()
        {
            if (_disposed) return;
            End(); _disposed = true; _ready = false;
            Unpatch();
            Status = "Stance compatibility stopped.";
        }
    }
}
