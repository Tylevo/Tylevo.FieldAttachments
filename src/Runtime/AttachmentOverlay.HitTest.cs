using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private enum SurfaceKind { None, Chrome, Comparison, Popup, Category, Candidate, Removal }

        // A visible surface exists even when its action is disabled. Preview and focus
        // use that surface; only Target may enter the existing press/release gesture.
        private readonly struct SurfaceHit
        {
            public readonly SurfaceKind Kind;
            public readonly AttachmentGroup? Group;
            public readonly PointerTarget? Target;
            public readonly ItemObservation? Item;
            public readonly PointerRow? Row;
            public SurfaceHit(SurfaceKind kind, AttachmentGroup? group = null, PointerTarget? target = null,
                ItemObservation? item = null, PointerRow? row = null)
            { Kind = kind; Group = group; Target = target; Item = item; Row = row; }
        }

        private SurfaceHit ResolveSurface(float x, float y)
        {
            if (_pointerState == null || CrossPresentationHidden || LayerTransitionActive) return default;
            // Footer controls are raised after the popup/comparison. The readouts and
            // opaque popup consume their bounds even when they have no click action.
            if (InsideComparisonRect(_layerControl, x, y))
                return new SurfaceHit(SurfaceKind.Chrome, target: LayerCycleEnabled ?
                    new PointerTarget { Kind = PointerKind.CycleLayer, Surface = "layer" + _displayLayer } : null);
            if (InsideMenuControls(x, y)) return new SurfaceHit(SurfaceKind.Chrome);
            if (InsideComparison(x, y)) return new SurfaceHit(SurfaceKind.Comparison);
            if (_popupKind != null && InsidePopup(x, y)) return ResolvePopupSurface(x, y);
            if (ExperimentalCrossSelectors) return ResolveCrossSurface(x, y);
            if (ExperimentalHexSelectors) return ResolveHexSurface(x, y);
            return ResolveCardSurface(x, y);
        }

        private SurfaceHit ResolvePopupSurface(float x, float y)
        {
            float px = x - _popup.anchoredPosition.x, py = y + _popup.anchoredPosition.y;
            PointerTarget? target = null;
            if (QuickSwapMenu.Close.Contains(px, py)) target = new PointerTarget { Kind = PointerKind.Close };
            else if (new SelectorRect(8, _popupFooter, 32, 30).Contains(px, py) && _popupPage > 0)
                target = new PointerTarget { Kind = PointerKind.Previous };
            else if (new SelectorRect(184, _popupFooter, 32, 30).Contains(px, py) && _popupPage + 1 < PopupPages)
                target = new PointerTarget { Kind = PointerKind.Next };
            else if (_availabilityButton.gameObject.activeSelf && AvailabilityButtonRect.Contains(px, py))
                target = new PointerTarget { Kind = PointerKind.ToggleUnavailable, Group = _popupGroup,
                    Surface = _showUnavailable ? "back-to-parts" : "unavailable" };
            if (target != null) return new SurfaceHit(SurfaceKind.Popup, _popupGroup, target);
            if (_showUnavailable) return new SurfaceHit(SurfaceKind.Popup, _popupGroup);
            int index = QuickSwapMenu.Row(px, py);
            if (index < 0 || !_pointerRows[index].Root.gameObject.activeSelf)
                return new SurfaceHit(SurfaceKind.Popup, _popupGroup);
            var row = _pointerRows[index];
            return new SurfaceHit(_popupPage * QuickSwapMenu.PageSize + index == 0 ? SurfaceKind.Removal : SurfaceKind.Candidate,
                _popupGroup, row.Target, row.Item, row);
        }

        private SurfaceHit ResolveCrossSurface(float x, float y)
        {
            SurfaceHit gap = default;
            for (int g = _crossGroups.Length - 1; g >= 0; g--)
            {
                var panel = _crossGroups[g]; var group = _groups[g].Group;
                if (!panel.Visible || !panel.Root.gameObject.activeInHierarchy || panel.Projection == null ||
                    !SurfacePoint(panel.Projection, panel.Root, CrossWidth, CrossHeight, x, y, out float px, out float py)) continue;
                // The holder is transparent: gaps retain a mounting-point focus but
                // must not occlude a painted card underneath them.
                var bounds = UseStudioCrossLayout ? new SelectorRect(0, 0, CrossWidth, CrossHeight) : CrossSelectorLayout.Group(AttachmentLayers.Home(group));
                if (gap.Kind == SurfaceKind.None && bounds.Contains(px, py)) gap = new SurfaceHit(SurfaceKind.Category, group);
                for (int i = panel.Cells.Length - 1; i >= 0; i--)
                {
                    var cell = panel.Cells[i];
                    if (!cell.Root.gameObject.activeSelf || !CrossCellRect(group, i).Contains(px, py)) continue;
                    return new SurfaceHit(i == 3 ? SurfaceKind.Removal : i == 1 || i == 2 ? SurfaceKind.Candidate : SurfaceKind.Category,
                        group, cell.Target, cell.Item);
                }
                var header = UseStudioCrossLayout ? StudioCrossLayout.Header : CrossSelectorLayout.Header(AttachmentLayers.Home(group));
                if (header.Contains(px, py)) return new SurfaceHit(SurfaceKind.Category, group, panel.HeaderTarget);
            }
            return gap;
        }

        private SurfaceHit ResolveHexSurface(float x, float y)
        {
            SurfaceHit gap = default;
            for (int g = _hexPanels.Length - 1; g >= 0; g--)
            {
                var panel = _hexPanels[g]; var group = _groups[g].Group;
                if (!panel.Root.gameObject.activeInHierarchy || !SurfacePoint(_groups[g].Projection, panel.Root,
                    HexSelectorLayout.Width, HexSelectorLayout.Height, x, y, out float px, out float py)) continue;
                if (gap.Kind == SurfaceKind.None) gap = new SurfaceHit(SurfaceKind.Category, group);
                for (int i = panel.Cells.Length - 1; i >= 0; i--)
                {
                    var cell = panel.Cells[i];
                    if (!cell.Shape.gameObject.activeSelf || !HexSelectorLayout.Contains(i, px, py)) continue;
                    return new SurfaceHit(i == 5 ? SurfaceKind.Removal : i >= 2 && i <= 4 ? SurfaceKind.Candidate : SurfaceKind.Category,
                        group, cell.Target, cell.Item);
                }
            }
            return gap;
        }

        private SurfaceHit ResolveCardSurface(float x, float y)
        {
            for (int g = _groups.Length - 1; g >= 0; g--)
            {
                var panel = _groups[g];
                if (!panel.Root.gameObject.activeInHierarchy || !AttachmentLayers.IsMenuGroup(panel.Group) ||
                    !SurfacePoint(panel.Projection, panel.Root, OverlayLayout.PanelWidth, OverlayLayout.PanelHeight,
                        x, y, out float px, out float py)) continue;
                var slot = _pointerState!.Current(panel.Group);
                if (slot != null)
                {
                    if (py <= 55 || py >= 61 && py <= 161 && px >= 12 && px <= 108)
                        return new SurfaceHit(SurfaceKind.Category, panel.Group,
                            new PointerTarget { Kind = PointerKind.Candidates, Group = panel.Group });
                    for (int i = panel.Candidates.Length - 1; i >= 0; i--)
                    {
                        var card = panel.Candidates[i];
                        if (!card.Background.gameObject.activeSelf || !new SelectorRect(116 + i * 104, 61, 96, 100).Contains(px, py)) continue;
                        PointerTarget? target = card.Item != null && ActionDenial(slot, card.Item, false,
                            slot.Candidates.Find(c => c.Item.Id == card.Item.Id)?.Evidence).Length == 0 ?
                            new PointerTarget { Kind = PointerKind.ChooseItem, Action = AttachmentRequest.ChoiceAction(slot.Slot),
                                Group = panel.Group, Path = slot.Slot.Path, Item = card.Item.Id } : null;
                        return new SurfaceHit(SurfaceKind.Candidate, panel.Group, target, card.Item);
                    }
                }
                // The rectangular panel has an opaque backplane, unlike Cross/Hex gaps.
                return new SurfaceHit(SurfaceKind.Category, panel.Group);
            }
            return default;
        }

        private static bool SurfacePoint(PanelProjection? projection, RectTransform root, float width, float height,
            float x, float y, out float px, out float py)
        {
            px = py = 0;
            if (projection != null)
            {
                if (!projection.TryUnmap(x, y, out float u, out float v)) return false;
                px = u * width; py = v * height;
            }
            else { px = x - root.anchoredPosition.x; py = y + root.anchoredPosition.y; }
            return px >= 0 && px <= width && py >= 0 && py <= height;
        }
    }
}
