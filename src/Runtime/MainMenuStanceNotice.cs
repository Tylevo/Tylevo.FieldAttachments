using System;
using EFT.UI;
using EFT.UI.Screens;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tylevo.FieldAttachments.Runtime
{
    // The native menu owns visibility and mouse input. No gameplay cursor capture or
    // new EventSystem is used, and closing the native screen disposes its notice.
    public sealed class MainMenuStanceNotice : IDisposable
    {
        private readonly Func<StanceNoticeContent> _content;
        private readonly Func<bool> _enabled;
        private readonly Action<string> _log;
        private readonly StanceNoticeSession _session = new StanceNoticeSession();
        private MenuScreen? _menu;
        private MenuScreen.MainMenuScreenController? _controller;
        private EftScreenManager? _manager;
        private StanceStatusNotice? _view;
        private StanceNoticeContent? _shownContent;
        private bool _initialized, _disposed, _failed;
        private float _nextContentRefresh;

        public MainMenuStanceNotice(Func<StanceNoticeContent> content, Func<bool> enabled, Action<string> log)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Initialize()
        {
            if (_initialized || _disposed || Application.isBatchMode) return;
            _initialized = true;
            // These native lifecycle members were inspected in EFT 40743.
            if (typeof(MenuScreen).Module.ModuleVersionId.ToString() != NativeInstallProbe.InspectedGameMvid)
            { Fail(new InvalidOperationException("unsupported native menu build")); return; }
            Tick();
        }

        public void Tick()
        {
            if (!_initialized || _disposed || _failed || Application.isBatchMode) return;
            try
            {
                if (!CurrentMenu())
                {
                    Detach();
                    AttachCurrentMenu();
                }
                bool show = CanInteract() && _session.CanShow(true, _enabled(), false);
                if (!show) { _view?.SetVisible(false); return; }
                if (_view == null)
                {
                    _view = new StanceStatusNotice(_menu!.transform, OpenForge, Dismiss);
                    _nextContentRefresh = 0;
                }
                if (_shownContent == null || Time.unscaledTime >= _nextContentRefresh)
                {
                    StanceNoticeContent content = _content();
                    if (!SameContent(_shownContent, content)) _view.Render(content);
                    _shownContent = content;
                    _nextContentRefresh = Time.unscaledTime + .25f;
                }
                _view.SetVisible(true);
            }
            catch (Exception e) { Fail(e); }
        }

        private void AttachCurrentMenu()
        {
            // Do not create native singleton infrastructure during startup/loading.
            if (!CommonUI.Instantiated || CommonUI.Instance == null) return;
            MenuScreen menu = CommonUI.Instance.MenuScreen;
            if (menu == null || !menu.gameObject.activeInHierarchy) return;
            EftScreenManager manager = EftScreenManager.Instance;
            var controller = manager.CurrentScreenController as MenuScreen.MainMenuScreenController;
            if (controller == null || controller.GetType() != typeof(MenuScreen.MainMenuScreenController) ||
                !controller.Active || controller.Closed) return;
            _menu = menu; _manager = manager; _controller = controller;
            controller.OnClose += NativeMenuClosed;
            manager.OnScreenChanged += NativeScreenChanged;
        }

        private bool CurrentMenu() => _menu != null && _controller != null && _manager != null &&
            _menu.gameObject.activeInHierarchy && _controller.Active && !_controller.Closed &&
            ReferenceEquals(_manager.CurrentScreenController, _controller) &&
            CommonUI.Instantiated && CommonUI.Instance != null && ReferenceEquals(CommonUI.Instance.MenuScreen, _menu);

        private bool CanInteract()
        {
            if (!CurrentMenu() || !Application.isFocused ||
                !Cursor.visible || Cursor.lockState == CursorLockMode.Locked) return false;
            EventSystem events = EventSystem.current;
            if (events == null || !events.isActiveAndEnabled || events.currentInputModule == null ||
                !events.currentInputModule.isActiveAndEnabled) return false;
            CanvasGroup group = _menu!.GetComponent<CanvasGroup>();
            if (group != null && (!group.interactable || !group.blocksRaycasts || group.alpha < .99f)) return false;
            CommonUI common = CommonUI.Instance;
            if (Active(common.HideoutScreenRear) || Active(common.HideoutScreenOverlay)) return false;
            if (!PreloaderUI.Instantiated || PreloaderUI.Instance == null) return true;
            PreloaderUI preloader = PreloaderUI.Instance;
            return !Active(preloader._loader) && !Active(preloader._hideoutLoadingScreen) &&
                !Active(preloader._pveLoadingScreen) && !Active(preloader._raidStartIntro) &&
                !(preloader._overlapBlackImage != null && preloader._overlapBlackImage.isActiveAndEnabled &&
                    preloader._overlapBlackImage.color.a > .01f);
        }

        private static bool Active(Component? component) => component != null && component.gameObject.activeInHierarchy;
        private static bool Active(GameObject? obj) => obj != null && obj.activeInHierarchy;

        private void NativeMenuClosed() => Detach();
        private void NativeScreenChanged(EEftScreenType ignored)
        {
            try { if (!CurrentMenu()) Detach(); }
            catch (Exception e) { Fail(e); }
        }

        private void OpenForge()
        {
            try
            {
                if (_disposed || _failed || _view?.IsVisible != true || !CanInteract()) return;
                StanceNoticeContent content = _content();
                if (!_session.TryOpenLink(true, _enabled(), Application.isBatchMode, content.ForgeUrl)) return;
                try
                {
                    Application.OpenURL(content.ForgeUrl);
                    _session.Dismiss();
                    _view?.SetVisible(false);
                }
                catch (Exception e) { _session.LinkFailed(); Log("Could not open the Stance Overhaul page: " + e.GetBaseException().Message); }
            }
            catch (Exception e) { Fail(e); }
        }

        private void Dismiss()
        {
            try
            {
                if (_disposed || _failed || _view?.IsVisible != true || !CanInteract() ||
                    !_session.CanShow(true, _enabled(), Application.isBatchMode)) return;
                _session.Dismiss();
                _view.SetVisible(false);
            }
            catch (Exception e) { Fail(e); }
        }

        private static bool SameContent(StanceNoticeContent? a, StanceNoticeContent b) => a != null &&
            a.Heading == b.Heading && a.Detail == b.Detail && a.ForgeUrl == b.ForgeUrl &&
            a.LinkLabel == b.LinkLabel && a.Ready == b.Ready;

        private void Detach()
        {
            // Clear our references first; native close events and destruction can re-enter.
            var controller = _controller; var manager = _manager; var view = _view;
            _controller = null; _manager = null; _menu = null; _view = null; _shownContent = null;
            try { if (controller != null) controller.OnClose -= NativeMenuClosed; } catch { }
            try { if (manager != null) manager.OnScreenChanged -= NativeScreenChanged; } catch { }
            try { view?.Dispose(); } catch { }
        }

        private void Fail(Exception exception)
        {
            if (!_failed) Log("Main-menu stance notice unavailable: " + exception.GetBaseException().Message);
            _failed = true;
            Detach();
        }

        private void Log(string message) { try { _log(message); } catch { } }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Detach();
        }
    }
}
