using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UniversalWaterSystem;
using RescueSystem;

/// <summary>
/// 游戏关卡管理器，挂到场景中一个空物体上
/// 负责：开始前冻船 → 玩家点击开始 → 解冻/显示说明/激活漩涡
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("开始菜单 UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button     startButton;

    [Header("说明 UI")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TMP_Text   tutorialText;

    private bool _gameStarted     = false;
    private bool _anchorDropped   = false;
    private bool _anchorRaised    = false;
    private bool _vortexPassed    = false;
    private bool _vortexActivated = false;

    private static readonly WaitForSeconds WaitPoint2 = new(0.2f);

    [Header("船")]
    [SerializeField] private ShipDynamics shipDynamics;
    [SerializeField] private Transform    shipTransform;

    [Header("开始后激活 / 禁用的对象")]
    [SerializeField] private GameObject[] objectsToEnableOnStart;
    [SerializeField] private GameObject[] objectsToDisableOnStart;

    [Header("失败 UI")]
    [SerializeField] private GameObject diePanel;

    [Header("场景切换")]
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private float       fadeDuration = 1.5f;
    [SerializeField] private Canvas      mainCanvas;   // 挂载所有 Panel 的 Canvas
    [SerializeField] private GameObject  envObject;    // fade 完成后激活的环境对象

    [Header("动物")]
    [SerializeField] private GameObject floatingAnimal;
    [SerializeField] private float      animalForwardDistance = 300f;
    [SerializeField] private float      animalLeftDistance    = 300f;

    [Header("Tree")]
    [SerializeField] private GameObject treeObject;
    [SerializeField] private float      treeForwardDistance = 500f;
    [SerializeField] private float      treeScaleDuration   = 2f;

    [Header("海啸")]
    [SerializeField] private Tsunami tsunami;

    [Header("漩涡")]
    [SerializeField] private WaterVortex vortex;
    [SerializeField] private float       vortexForwardDistance = 500f;
    [SerializeField] private float       vortexSpawnDuration   = 5f;

    // ── 生命周期 ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (shipDynamics != null) shipDynamics.Freeze();
    }

    void OnEnable()
    {
        WaterVortex.OnPlayerReachedBottom += OnPlayerFailed;
        BoatRescueManager.OnAnimalRescued += OnAnimalRescued;
        Tsunami.OnPlayerHit               += OnTsunamiImpactComplete;
    }

    void OnDisable()
    {
        WaterVortex.OnPlayerReachedBottom -= OnPlayerFailed;
        BoatRescueManager.OnAnimalRescued -= OnAnimalRescued;
        Tsunami.OnPlayerHit               -= OnTsunamiImpactComplete;
    }

    void OnPlayerFailed() => ShowDiePanel();

    public void ShowDiePanel()
    {
        Time.timeScale = 0f;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);

        if (diePanel != null)
        {
            diePanel.SetActive(true);
            if (!diePanel.TryGetComponent<Button>(out var btn))
                btn = diePanel.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                diePanel.SetActive(false);
                RestartScene();
            });
        }
        else
        {
            RestartScene();
        }
    }

    void OnTsunamiImpactComplete()
    {
        StartCoroutine(FadeAndSwitch());
    }

    System.Collections.IEnumerator FadeAndSwitch()
    {
        // 淡出到黑
        yield return StartCoroutine(Fade(0f, 1f));

        // 激活 Env
        if (envObject != null) envObject.SetActive(true);

        // 延迟 0.2s 后重置船的 transform
        yield return WaitPoint2;
        if (shipTransform != null)
        {
            //if (shipDynamics != null) shipDynamics.ClearForces();
            shipTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        // 销毁引导阶段的物体
        if (vortex        != null) Destroy(vortex.gameObject);
        if (tsunami       != null) Destroy(tsunami.gameObject);
        if (treeObject    != null) Destroy(treeObject);
        if (floatingAnimal != null) Destroy(floatingAnimal);

        // 隐藏 Canvas 下所有 Panel
        if (mainCanvas != null)
            foreach (Transform child in mainCanvas.transform)
                child.gameObject.SetActive(false);

        yield return null;

        // 从黑淡入
        yield return StartCoroutine(Fade(1f, 0f));
    }

    System.Collections.IEnumerator Fade(float from, float to)
    {
        if (fadePanel == null) yield break;
        fadePanel.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed        += Time.deltaTime;
            fadePanel.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        fadePanel.alpha = to;
        if (to == 0f) fadePanel.gameObject.SetActive(false);
    }

    void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Start()
    {
        if (menuPanel != null)    menuPanel.SetActive(true);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (startButton != null)  startButton.onClick.AddListener(OnStartClicked);

        if (vortex != null) vortex.gameObject.SetActive(false);

        foreach (var obj in objectsToEnableOnStart)
            if (obj != null) obj.SetActive(false);
    }

    // ── 开始游戏 ──────────────────────────────────────────────────────────────

    public void OnStartClicked()
    {
        // 隐藏开始菜单
        if (menuPanel != null) menuPanel.SetActive(false);

        // 解冻船
        if (shipDynamics != null) shipDynamics.Unfreeze();

        // 激活 / 禁用场景对象
        foreach (var obj in objectsToEnableOnStart)
            if (obj != null) obj.SetActive(true);
        foreach (var obj in objectsToDisableOnStart)
            if (obj != null) obj.SetActive(false);

        // 显示说明 UI
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        _gameStarted = true;
    }

    // ── 输入检测 ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_gameStarted) return;

        // 快捷跳过：按 0 直接进入生成树+触发海啸阶段
        if (Input.GetKeyDown(KeyCode.Alpha0) && !_vortexPassed)
        {
            _anchorDropped = true;
            _anchorRaised  = true;
            _vortexPassed  = true;
            OnAnimalRescued();
        }

        // 阶段1：等待下锚
        if (!_anchorDropped && (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)))
        {
            _anchorDropped = true;
            if (tutorialText != null)
                tutorialText.text = "R : Raise Anchor";
        }

        // 阶段2：等待起锚
        if (_anchorDropped && !_anchorRaised && Input.GetKeyDown(KeyCode.R))
        {
            _anchorRaised = true;
            if (tutorialText != null)
                tutorialText.text = "Use your anchor to \nescape the whirlpool!";
            ActivateVortexAhead();
        }

        // 阶段3：检测是否越过漩涡
        if (_anchorRaised && !_vortexPassed && vortex != null && shipTransform != null)
        {
            // 漩涡在船的后方时（点积为负）视为已越过
            Vector3 toVortex = vortex.transform.position - shipTransform.position;
            if (Vector3.Dot(toVortex, shipTransform.forward) < 0f)
            {
                _vortexPassed = true;
                if (tutorialText != null)
                    tutorialText.text = "Animal's drowning!\nPick it up!";
                SpawnFloatingAnimal();
            }
        }
    }

    // ── 说明 UI ───────────────────────────────────────────────────────────────

    public void HideTutorial()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    // ── 漩涡 ──────────────────────────────────────────────────────────────────

    void ActivateVortexAhead()
    {
        if (vortex == null || shipTransform == null) return;

        if (_vortexActivated) return;
        _vortexActivated = true;

        Vector3 pos = shipTransform.position + shipTransform.forward * vortexForwardDistance;
        pos.y       = vortex.transform.position.y;
        vortex.transform.position = pos;
        vortex.gameObject.SetActive(true);

        StartCoroutine(SpawnVortexGradually());
    }

    System.Collections.IEnumerator SpawnVortexGradually()
    {
        vortex.SetIntensity(0f);
        float elapsed = 0f;

        while (elapsed < vortexSpawnDuration)
        {
            elapsed += Time.deltaTime;
            vortex.SetIntensity(elapsed / vortexSpawnDuration);
            yield return null;
        }

        vortex.SetIntensity(1f);
    }

    // ── 动物 ──────────────────────────────────────────────────────────────────

    void SpawnFloatingAnimal()
    {
        if (floatingAnimal == null || shipTransform == null) return;

        Vector3 pos = shipTransform.position
                    + shipTransform.forward * animalForwardDistance
                    - shipTransform.right   * animalLeftDistance;
        pos.y = floatingAnimal.transform.position.y;

        floatingAnimal.transform.position = pos;
        floatingAnimal.SetActive(true);
    }

    // ── Tree ──────────────────────────────────────────────────────────────────

    void OnAnimalRescued()
    {
        if (tutorialText != null)
            tutorialText.text = "Now\nTo the New World!";

        if (treeObject == null || shipTransform == null) return;

        Vector3 pos = shipTransform.position + shipTransform.forward * treeForwardDistance;
        pos.y = treeObject.transform.position.y;
        treeObject.transform.position = pos;

        StartCoroutine(SpawnTreeGradually());
    }

    System.Collections.IEnumerator SpawnTreeGradually()
    {
        if (tsunami != null && shipTransform != null)
            tsunami.TriggerTsunamiAheadOf(shipTransform.position, shipTransform.forward);

        Vector3 targetScale = treeObject.transform.localScale == Vector3.zero
            ? Vector3.one
            : treeObject.transform.localScale;

        treeObject.transform.localScale = Vector3.zero;
        treeObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < treeScaleDuration)
        {
            elapsed += Time.deltaTime;
            treeObject.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, elapsed / treeScaleDuration);
            yield return null;
        }

        treeObject.transform.localScale = targetScale;
    }
}
