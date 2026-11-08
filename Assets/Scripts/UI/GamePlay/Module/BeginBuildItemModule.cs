using Framework.UI;
using UnityEngine;
using Framework.Res;

public partial class BeginBuildItemModule : BaseModule
{
    private UIFeedbackController feedback;
    private string imagePath;

    public void SetImage(string imageName)
    {
        feedback?.ResetFeedback();
        comps.imgCardVisual.sprite = null;
        ReleaseImage();
        if (string.IsNullOrEmpty(imageName)) return;
        string path = $"GamePlay/CardBuild/{imageName}";
        comps.imgCardVisual.sprite = ResManager.Instance.Load<Sprite>(path);
        if (comps.imgCardVisual.sprite != null) imagePath = path;
    }

    private void ReleaseImage()
    {
        if (imagePath == null) return;
        ResManager.Instance.UnloadAsset<Sprite>(imagePath);
        imagePath = null;
    }
    protected override void OnBind()
    {
        feedback = GetComponent<UIFeedbackController>();
    }

    protected override void OnUnBind()
    {
        if (comps.imgCardVisual != null) comps.imgCardVisual.sprite = null;
        ReleaseImage();
    }
}
