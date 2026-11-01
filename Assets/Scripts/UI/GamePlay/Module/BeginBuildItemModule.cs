using Framework.UI;
using UnityEngine;
using UnityEngine.UI;
using Framework.Res;

public class BeginBuildItemModule : BaseModule
{
    private string imagePath;

    public void SetImage(string imageName)
    {
        ReleaseImage();
        var image = GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = null;
        if (string.IsNullOrEmpty(imageName)) return;
        string path = $"GamePlay/CardBuild/{imageName}";
        image.sprite = ResManager.Instance.Load<Sprite>(path);
        if (image.sprite != null) imagePath = path;
    }

    private void ReleaseImage()
    {
        if (imagePath == null) return;
        ResManager.Instance.UnloadAsset<Sprite>(imagePath);
        imagePath = null;
    }
    protected override void OnBind()
    {
        
    }

    protected override void OnUnBind()
    {
        ReleaseImage();
    }
}
