using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始菜单 UI 入口，仅负责把按钮点击转发给 GameManager
/// 所有游戏逻辑在 GameManager 中
/// </summary>
public class StartMenuUI : MonoBehaviour
{
    [SerializeField] private Button startButton;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.OnStartClicked();
            });
    }
}
