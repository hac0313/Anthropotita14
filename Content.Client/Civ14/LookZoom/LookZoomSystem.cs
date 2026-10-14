#nullable enable
using Content.Shared.Camera;
using Content.Shared.Input;
using Content.Shared.Hands;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Input.Binding;
using Content.Client.Movement.Components;
using Content.Client.Movement.Systems;
using Robust.Shared.Player;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;
using Robust.Client.Timing;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.Client.Civ14.LookZoom;
public sealed class LookZoomSystem : EntitySystem
{
    [Dependency] private readonly EyeCursorOffsetSystem _eyeOffset = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IClientGameTiming _gameTiming = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LookZoomComponent, GetEyeOffsetRelayedEvent>(UpdateLookZoom);
        SubscribeAllEvent<RequestUseInHandEvent>(OnUseItemInHand);

        CommandBinds.Builder
        .Bind(ContentKeyFunctions.LookZoom, InputCmdHandler.FromDelegate(OnLookZoomHandler, handle: false, outsidePrediction: false))
        .Register<LookZoomSystem>();

        _inputManager.FirstChanceOnKeyEvent += OnFirstChanceKeyEvent;
    }

    public override void Shutdown()
    {
        _inputManager.FirstChanceOnKeyEvent -= OnFirstChanceKeyEvent;
        base.Shutdown();
    }

    // The LookZoom keybind is registered with canFocus, so the engine releases
    // keyboard focus on key-down before command handlers run. That would both
    // zoom while typing and drop the typed character, since the released focus
    // leaves the follow-up text-input event with no target. Consume a matching
    // key event while a text entry control is focused so the engine neither
    // releases focus nor runs the command, and the character inserts normally.
    private void OnFirstChanceKeyEvent(KeyEventArgs args, KeyEventType type)
    {
        if (type == KeyEventType.Up)
            return;

        if (_uiManager.KeyboardFocused is not (LineEdit or TextEdit))
            return;

        if (IsLookZoomKey(args))
            args.Handle();
    }

    private bool IsLookZoomKey(KeyEventArgs args)
    {
        if (!_inputManager.TryGetKeyBinding(ContentKeyFunctions.LookZoom, out var binding))
            return false;

        if (args.Key != binding.BaseKey)
            return false;

        return ModMatches(binding.Mod1, args)
            && ModMatches(binding.Mod2, args)
            && ModMatches(binding.Mod3, args);
    }

    private static bool ModMatches(Keyboard.Key mod, KeyEventArgs args)
    {
        return mod switch
        {
            Keyboard.Key.Unknown => true,
            Keyboard.Key.Control => args.Control,
            Keyboard.Key.Shift => args.Shift,
            Keyboard.Key.Alt => args.Alt,
            Keyboard.Key.LSystem or Keyboard.Key.RSystem => args.System,
            _ => false,
        };
    }

    public void OnLookZoomHandler(ICommonSession? session)
    {
        // Don't trigger zoom while typing text (e.g. in the chat bar). The key
        // event is normally consumed before it gets here; this is a fallback in
        // case focus changed in between.
        if (_uiManager.KeyboardFocused is LineEdit or TextEdit)
            return;

        // Gets the player character uid
        var uid = session?.AttachedEntity;

        if (!TryComp<LookZoomComponent>(uid, out var comp))
            return;

        // If the active item has EyeCursorOffset but is not a weapon (e.g. binoculars),
        // don't trigger zoom via Space. Such items are activated through item interaction instead.
        if (_handsSystem.TryGetActiveItem(uid.Value, out var item) &&
            TryComp<EyeCursorOffsetComponent>(item, out _) &&
            !HasComp<GunComponent>(item))
        {
            return;
        }

        ToggleZoom(uid.Value, comp);
    }

    private void OnUseItemInHand(RequestUseInHandEvent msg, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;

        if (!TryComp<LookZoomComponent>(uid, out var comp))
            return;

        // Only trigger zoom via interaction if the active item has EyeCursorOffset (binoculars)
        if (!_handsSystem.TryGetActiveItem(uid.Value, out var item) ||
            !TryComp<EyeCursorOffsetComponent>(item, out _))
        {
            return;
        }

        ToggleZoom(uid.Value, comp);
    }

    private void ToggleZoom(EntityUid uid, LookZoomComponent comp)
    {
        // Checks if the cooldown is over by comparing to the current time
        if (_timing.CurTime < comp.DelayedTime)
            return;

        // Sets the cooldown to the current time + 0.1 seconds
        comp.DelayedTime = _timing.CurTime + TimeSpan.FromSeconds(0.1);

        if (comp.State == false)
        {
            _handsSystem.TryGetActiveItem(uid, out var item);
            SnapOffset(uid);
            SnapOffset(item);
            comp.State = true;
            return;
        }
        comp.State = false;
    }

    public void UpdateLookZoom(EntityUid uid, LookZoomComponent comp, ref GetEyeOffsetRelayedEvent args)
    {
        if (comp.State == false)
        {
            return;
        }

        _handsSystem.TryGetActiveItem(uid, out var item);

        // Sets the offset if there is an item in the active hand with the EyeCurserOffset component
        if (item != null && TryComp<EyeCursorOffsetComponent>(item, out var itemComp))
        {
            SetOffset(item.Value, args);
            return;
        }

        //Sets the offset using the EyeCurserOffset on the player entity instead
        SetOffset(uid, args);
    }

    private void SetOffset(EntityUid uid, GetEyeOffsetRelayedEvent args)
    {
        var offset = _eyeOffset.OffsetAfterMouse(uid, null);
        if (offset == null)
            return;

        args.Offset += offset.Value;
    }

    private void SnapOffset(EntityUid? uid)
    {
        // Snap the offset to the cursor immediately instead of resetting it to zero,
        // otherwise the view (and any shots) keep aiming at the pre-zoom cursor
        // position until the gradual pan catches up.
        if (_gameTiming.IsFirstTimePredicted)
            _eyeOffset.SnapOffsetToMouse(uid);
    }
}