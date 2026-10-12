using System.Numerics;
using Content.Client.Movement.Components;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Map;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Movement.Systems;

public sealed partial class EyeCursorOffsetSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;

    // This value is here to make sure the user doesn't have to move their mouse
    // all the way out to the edge of the screen to get the full offset.
    static private float _edgeOffset = 0.9f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeCursorOffsetComponent, GetEyeOffsetEvent>(OnGetEyeOffsetEvent);
    }

    private void OnGetEyeOffsetEvent(EntityUid uid, EyeCursorOffsetComponent component, ref GetEyeOffsetEvent args)
    {
        var offset = OffsetAfterMouse(uid, component);
        if (offset == null)
            return;

        args.Offset += offset.Value;
    }

    public Vector2? OffsetAfterMouse(EntityUid uid, EyeCursorOffsetComponent? component)
    {
        var localPlayer = _player.LocalPlayer?.ControlledEntity;

        if (localPlayer == null)
            return null;

        component ??= EnsureComp<EyeCursorOffsetComponent>(uid);

        // Doesn't move the offset if the mouse has left the game window!
        if (_inputManager.MouseScreenPosition.Window == WindowId.Invalid)
            return component.CurrentPosition;

        if (!TryGetMouseOffsetTarget(component, out var target))
            return component.CurrentPosition;

        component.TargetPosition = target;

        //Makes the view not jump immediately when moving the cursor fast.
        if (component.CurrentPosition != component.TargetPosition)
        {
            Vector2 vectorOffset = component.TargetPosition - component.CurrentPosition;
            if (vectorOffset.Length() > component.OffsetSpeed)
            {
                vectorOffset = vectorOffset.Normalized() * component.OffsetSpeed;
            }
            // Check to make sure vectorOffset is not NaN
            if (float.IsNaN(vectorOffset.X) || float.IsNaN(vectorOffset.Y))
                return null;

            component.CurrentPosition += vectorOffset;
        }
        return component.CurrentPosition;
    }

    /// <summary>
    ///     Instantly moves the eye offset to where the cursor currently points, skipping the gradual pan.
    ///     Used when toggling zoom so that the view (and any shots fired) immediately match the cursor.
    /// </summary>
    public void SnapOffsetToMouse(EntityUid? uid, EyeCursorOffsetComponent? component = null)
    {
        if (uid == null)
            return;

        component ??= EnsureComp<EyeCursorOffsetComponent>(uid.Value);

        if (!TryGetMouseOffsetTarget(component, out var target))
            return;

        component.TargetPosition = target;
        component.CurrentPosition = target;
    }

    private bool TryGetMouseOffsetTarget(EyeCursorOffsetComponent component, out Vector2 target)
    {
        target = default;

        var mousePos = _inputManager.MouseScreenPosition;

        // The game viewport does not necessarily fill the whole window (e.g. the Separated HUD layout
        // puts the chat panel next to it), so the offset has to be relative to the viewport's actual
        // screen rect instead of the window size.
        var viewport = _eyeManager.MainViewport as Control;
        var viewportSize = (Vector2) (viewport?.PixelSize ?? _clyde.MainWindow.Size);
        var viewportCenter = (Vector2) (viewport?.GlobalPixelPosition ?? Vector2i.Zero) + viewportSize / 2f;

        var minValue = MathF.Min(viewportSize.X / 2, viewportSize.Y / 2) * _edgeOffset;
        if (minValue <= 0f)
            return false;

        var mouseNormalizedPos = new Vector2(-(mousePos.X - viewportCenter.X) / minValue, (mousePos.Y - viewportCenter.Y) / minValue); // X needs to be inverted here for some reason, otherwise it ends up flipped.

        // The offset must account for the in-world rotation.
        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        target = Vector2.Transform(mouseNormalizedPos, System.Numerics.Quaternion.CreateFromAxisAngle(-System.Numerics.Vector3.UnitZ, (float)(eyeRotation.Opposite().Theta))); // I don't know, it just works.

        // Caps the offset into a circle around the player.
        target *= component.MaxOffset;
        if (target.Length() > component.MaxOffset)
        {
            target = target.Normalized() * component.MaxOffset;
        }

        return true;
    }
}
