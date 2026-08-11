using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// UI 模组基类 —— 挂载到模组预制体上，实现自身的控件绑定
    ///
    /// 继承此类创建自定义模组（如 ButtonModule、ItemSlotModule）
    /// 模组预制体的子物体用 btn_xxx / txt_xxx / img_xxx 命名
    /// Export Code 后通过 module.comps.xxx 访问控件
    ///
    /// 面板中使用时：
    ///   comps.modButton.comps.imgIcon → 拿到模组内的控件
    /// </summary>
    public abstract class BaseModule : MonoBehaviour
    {
        /// <summary> 是否已绑定 </summary>
        [System.NonSerialized] private bool _bound;

        private void Awake()
        {
            if (_bound) return;
            _bound = true;

            var binder = GetComponent<UIBinder>();
            if (binder != null)
            {
                binder.Bind();
                OnBind();
            }
        }

        private void OnDestroy()
        {
            OnUnBind();
            _bound = false;
        }

        /// <summary> 控件绑定完成后（子类必须实现） </summary>
        protected abstract void OnBind();

        /// <summary> 销毁前（子类必须实现） </summary>
        protected abstract void OnUnBind();
    }
}
