using System;
using Framework;
using Framework.UI;

public static class SecondConfirmHelper
{
    public static void ConfirmQuit<T>(T source, Action beforeQuit = null) where T : BasePanel
    {
        OpenFrom(source, "ui_confirm_quit_tip", () =>
        {
            beforeQuit?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
            return true;
        }, closeBeforeConfirm: false);
    }

    /// <summary>从来源面板打开确认框。取消或确认操作未完成时恢复来源面板。</summary>
    public static void OpenFrom<T>(T source, string tipId, Func<bool> onConfirm) where T : BasePanel =>
        OpenFrom(source, tipId, onConfirm, closeBeforeConfirm: true);

    private static void OpenFrom<T>(T source, string tipId, Func<bool> onConfirm, bool closeBeforeConfirm) where T : BasePanel
    {
        var ui = UIManager.Instance;
        var existing = ui.GetPanel<SecondConfirmPanel>();
        if (source == null || !source.IsOpen || source.IsTransitioning || SceneController.Instance.IsLoading
            || (existing != null && (existing.IsOpen || existing.IsTransitioning))) return;

        var menu = source as MenuPanel;
        menu?.KeepBackground();
        source.HideSelf(() =>
        {
            if (!CanRestoreSource()) return;

            var panel = ui.Show<SecondConfirmPanel>(confirm =>
                confirm.Configure(tipId, menu == null, closeBeforeConfirm, Confirm, RestoreSource));
            if (panel == null) RestoreSource();
        });

        bool CanRestoreSource() => source != null && !source.IsOpen && !SceneController.Instance.IsLoading
            && ui.GetPanel<T>() == source;

        void RestoreSource()
        {
            if (CanRestoreSource()) ui.Show<T>();
        }

        void Confirm()
        {
            if (CanRestoreSource() && !onConfirm()) RestoreSource();
        }
    }
}
