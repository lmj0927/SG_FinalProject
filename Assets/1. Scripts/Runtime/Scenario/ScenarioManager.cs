using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum GamePhase
{
    MainScenario,
    MapExplore,
    SubScenario
}

/// <summary>
/// 메인/서브 시나리오와 맵 탐색 페이즈를 오케스트레이션합니다.
/// </summary>
public class ScenarioManager : MonoBehaviour
{
    [Header("Chapters")]
    [SerializeField] private List<ChapterEntry> chapters = new List<ChapterEntry>();
    [SerializeField] private int startChapterIndex;

    [Header("Character Button")]
    [SerializeField] private Button characterButton;
    [SerializeField] private Image characterButtonImage;

    [Header("References")]
    [SerializeField] private ScenarioRunner scenarioRunner;
    [SerializeField] private MapNavigator mapNavigator;
    [SerializeField] private Button nextChapterButton;

    [Header("Behavior")]
    [SerializeField] private bool startGameOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugSkip = true;
    [SerializeField] private KeyCode debugSkipToLastNodeKey = KeyCode.F9;

    private readonly List<SubScenarioProgress> currentSubs = new List<SubScenarioProgress>();

    private int currentChapterIndex;
    private GamePhase phase = GamePhase.MainScenario;
    private int activeSubIndex = -1;
    private int visibleSubIndex = -1;
    private BackgroundType savedMapLocation = BackgroundType.None;

    public GamePhase Phase => phase;
    public int CurrentChapterIndex => currentChapterIndex;
    public bool StartsGameOnStart => startGameOnStart;

    public event Action<int> OnChapterMainCompleted;

    private struct SubScenarioProgress
    {
        public SubScenarioEntry entry;
        public bool completed;
    }

    private void Awake()
    {
        if (characterButtonImage == null && characterButton != null)
        {
            characterButtonImage = characterButton.image;
        }

        if (characterButton != null)
        {
            characterButton.onClick.RemoveAllListeners();
            characterButton.onClick.AddListener(HandleCharacterButtonClicked);
        }

        HideCharacterButton();

        if (mapNavigator != null)
        {
            mapNavigator.OnLocationChanged += HandleLocationChanged;
        }

        BindScenarioEndedHandler();

        if (nextChapterButton != null)
        {
            nextChapterButton.onClick.RemoveAllListeners();
            nextChapterButton.onClick.AddListener(AdvanceToNextChapter);
        }
    }   

    private void BindScenarioEndedHandler()
    {
        if (scenarioRunner == null)
        {
            return;
        }

        scenarioRunner.OnScenarioEnded -= HandleScenarioEnded;
        scenarioRunner.OnScenarioEnded += HandleScenarioEnded;
    }

    private void Start()
    {
        if (startGameOnStart)
        {
            StartGame();
        }
    }

    private void Update()
    {
        if (!enableDebugSkip || debugSkipToLastNodeKey == KeyCode.None)
        {
            return;
        }

        if (Input.GetKeyDown(debugSkipToLastNodeKey))
        {
            DebugSkipToLastNode();
        }
    }

    /// <summary>
    /// 디버그용: 현재 재생 중인 메인/서브 시나리오의 마지막 노드로 스킵합니다.
    /// </summary>
    public void DebugSkipToLastNode()
    {
        if (scenarioRunner == null)
        {
            Debug.LogWarning("[ScenarioManager] ScenarioRunner가 연결되지 않았습니다.");
            return;
        }

        if (phase != GamePhase.MainScenario && phase != GamePhase.SubScenario)
        {
            Debug.LogWarning("[ScenarioManager] 시나리오 재생 중에만 스킵할 수 있습니다.");
            return;
        }

        if (!scenarioRunner.IsRunning)
        {
            Debug.LogWarning("[ScenarioManager] 실행 중인 시나리오가 없습니다.");
            return;
        }

        scenarioRunner.SkipToLastNode();
    }

    public void StartGame()
    {
        if (chapters == null || chapters.Count == 0)
        {
            Debug.LogWarning("[ScenarioManager] chapters가 비어 있습니다.");
            return;
        }

        if (scenarioRunner == null)
        {
            Debug.LogWarning("[ScenarioManager] ScenarioRunner가 연결되지 않았습니다.");
            return;
        }

        currentChapterIndex = Mathf.Clamp(startChapterIndex, 0, chapters.Count - 1);
        StartMainScenarioForCurrentChapter();
    }

    /// <summary>
    /// 다음 메인 챕터로 진행 (Main2 등). 조건은 추후 확장.
    /// </summary>
    public void AdvanceToNextChapter()
    {
        if (chapters == null || currentChapterIndex + 1 >= chapters.Count)
        {
            Debug.LogWarning("[ScenarioManager] 다음 챕터가 없습니다.");
            return;
        }

        currentChapterIndex++;
        StartMainScenarioForCurrentChapter();
    }

    private void StartMainScenarioForCurrentChapter()
    {
        ChapterEntry chapter = chapters[currentChapterIndex];
        if (chapter?.mainScenario == null)
        {
            Debug.LogWarning($"[ScenarioManager] 챕터 {currentChapterIndex} mainScenario가 없습니다.");
            return;
        }

        phase = GamePhase.MainScenario;
        activeSubIndex = -1;
        visibleSubIndex = -1;
        currentSubs.Clear();

        HideMapExploreUi();
        BindScenarioEndedHandler();
        scenarioRunner.StartScenario(chapter.mainScenario);
    }

    private void HandleScenarioEnded()
    {
        if (phase == GamePhase.MainScenario)
        {
            OnMainScenarioCompleted();
            return;
        }

        if (phase == GamePhase.SubScenario)
        {
            OnSubScenarioCompleted();
        }
    }

    private void OnMainScenarioCompleted()
    {
        OnChapterMainCompleted?.Invoke(currentChapterIndex);

        ChapterEntry chapter = chapters[currentChapterIndex];
        currentSubs.Clear();

        if (chapter.subScenarios != null)
        {
            for (int i = 0; i < chapter.subScenarios.Count; i++)
            {
                SubScenarioEntry entry = chapter.subScenarios[i];
                if (entry == null || entry.scenario == null)
                {
                    continue;
                }

                currentSubs.Add(new SubScenarioProgress
                {
                    entry = entry,
                    completed = false
                });
            }
        }

        phase = GamePhase.MapExplore;

        if (mapNavigator == null)
        {
            Debug.LogWarning("[ScenarioManager] MapNavigator가 연결되지 않았습니다.");
            return;
        }

        mapNavigator.EnterMapMode(chapter.mapStartAfterMain);
        RefreshCharacterButton();

        Debug.Log($"[ScenarioManager] 메인 시나리오 완료 → MapExplore, 위치={chapter.mapStartAfterMain}");
    }

    private void OnSubScenarioCompleted()
    {
        if (activeSubIndex >= 0 && activeSubIndex < currentSubs.Count)
        {
            SubScenarioProgress progress = currentSubs[activeSubIndex];
            progress.completed = true;
            currentSubs[activeSubIndex] = progress;
        }

        activeSubIndex = -1;
        visibleSubIndex = -1;
        phase = GamePhase.MapExplore;

        if (mapNavigator == null)
        {
            HideCharacterButton();
            return;
        }

        mapNavigator.EnterMapMode(savedMapLocation);
        RefreshCharacterButton();
    }

    private void HandleCharacterButtonClicked()
    {
        if (phase != GamePhase.MapExplore || visibleSubIndex < 0)
        {
            return;
        }

        StartSubScenario(visibleSubIndex);
    }

    private void StartSubScenario(int subIndex)
    {
        SubScenarioProgress progress = currentSubs[subIndex];
        if (progress.entry.scenario == null)
        {
            Debug.LogWarning("[ScenarioManager] 서브 ScenarioAsset이 없습니다.");
            return;
        }

        phase = GamePhase.SubScenario;
        activeSubIndex = subIndex;
        visibleSubIndex = -1;
        savedMapLocation = mapNavigator.CurrentLocation;

        HideMapExploreUi();
        scenarioRunner.StartScenario(progress.entry.scenario);
    }

    private void HandleLocationChanged(BackgroundType _)
    {
        RefreshCharacterButton();
    }

    private void RefreshCharacterButton()
    {
        visibleSubIndex = -1;
        HideCharacterButton();

        if (phase != GamePhase.MapExplore || mapNavigator == null || !mapNavigator.IsMapModeActive)
        {
            return;
        }

        int subIndex = FindSubIndexAtLocation(mapNavigator.CurrentLocation);
        if (subIndex < 0)
        {
            return;
        }

        CharacterType character = currentSubs[subIndex].entry.character;
        if (!ApplyCharacterButtonImage(character))
        {
            Debug.LogWarning($"[ScenarioManager] 캐릭터 버튼 이미지를 찾을 수 없습니다: {character}");
            return;
        }

        visibleSubIndex = subIndex;
        characterButton.gameObject.SetActive(true);
    }

    private int FindSubIndexAtLocation(BackgroundType location)
    {
        for (int i = 0; i < currentSubs.Count; i++)
        {
            SubScenarioProgress progress = currentSubs[i];
            if (progress.completed)
            {
                continue;
            }

            if (progress.entry.location == location)
            {
                return i;
            }
        }

        return -1;
    }

    private bool ApplyCharacterButtonImage(CharacterType character)
    {
        if (characterButtonImage == null)
        {
            return false;
        }

        ImageManager manager = ImageManager.Instance;
        if (manager == null)
        {
            return false;
        }

        if (!manager.TryGetCharacterImage(character, EmotionType.Default, out Sprite sprite))
        {
            return false;
        }

        characterButtonImage.sprite = sprite;
        characterButtonImage.enabled = true;
        return true;
    }

    private void HideMapExploreUi()
    {
        mapNavigator?.ExitMapMode();
        HideCharacterButton();
    }

    private void HideCharacterButton()
    {
        visibleSubIndex = -1;

        if (characterButton != null)
        {
            characterButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (mapNavigator != null)
        {
            mapNavigator.OnLocationChanged -= HandleLocationChanged;
        }

        if (scenarioRunner != null)
        {
            scenarioRunner.OnScenarioEnded -= HandleScenarioEnded;
        }

        if (characterButton != null)
        {
            characterButton.onClick.RemoveAllListeners();
        }

        OnChapterMainCompleted = null;
    }
}
