using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScenarioAsset을 재생합니다. nodes[0]부터 시작하며 Id/Next로 분기합니다.
/// </summary>
public class ScenarioRunner : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ScenarioAsset scenario;

    [Header("UI")]
    [SerializeField] private UI_TextDialog textDialog;
    [SerializeField] private UI_ChoicePanel choicePanel;
    [SerializeField] private UI_EnterInput enterInput;
    [SerializeField] private Image characterImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject nameObject;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Behavior")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool useLocalLMEcho = true;
    [SerializeField] private bool useLocalLMCbt = true;
    [SerializeField] private bool logEnterEchoDebug = true;

    private const string InputToken = "{input}";
    private const string CbtToken = "{cbt}";

    private readonly Dictionary<string, ScenarioData> nodeById = new Dictionary<string, ScenarioData>();

    private ScenarioData currentNode;
    private bool isRunning;
    private bool waitingForChoicePanel;
    private bool isProcessingEnter;
    private string lastEnterInput = string.Empty;
    private string lastCbtInput = string.Empty;

    public event Action OnScenarioEnded;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        BuildNodeLookup();
    }

    private void Start()
    {
        if (!playOnStart)
        {
            return;
        }

        // ScenarioManager가 StartGame()으로 시작하면 중복 실행하지 않습니다.
        ScenarioManager manager = FindObjectOfType<ScenarioManager>();
        if (manager != null && manager.StartsGameOnStart)
        {
            return;
        }

        StartScenario();
    }

    public void StartScenario(ScenarioAsset asset)
    {
        if (asset == null)
        {
            Debug.LogWarning("[ScenarioRunner] ScenarioAsset이 null입니다.");
            return;
        }

        scenario = asset;
        StartScenario();
    }

    public void StartScenario()
    {
        if (scenario == null || scenario.nodes == null || scenario.nodes.Count == 0)
        {
            Debug.LogWarning("[ScenarioRunner] ScenarioAsset 또는 nodes가 비어 있습니다.");
            return;
        }

        if (textDialog == null)
        {
            Debug.LogWarning("[ScenarioRunner] UI_TextDialog가 연결되지 않았습니다.");
            return;
        }

        BuildNodeLookup();
        UnsubscribeUiEvents();
        SubscribeUiEvents();

        isRunning = true;
        waitingForChoicePanel = false;
        isProcessingEnter = false;
        lastEnterInput = string.Empty;
        lastCbtInput = string.Empty;
        choicePanel?.Hide();
        enterInput?.Hide();

        textDialog.SetAutoHideOnClick(false);
        PlayNode(scenario.nodes[0]);
    }

    public void StopScenario()
    {
        if (!isRunning)
        {
            return;
        }

        FinishScenario();
    }

    /// <summary>
    /// 디버그용: 현재 시나리오의 마지막 노드(또는 End 노드)로 즉시 이동합니다.
    /// </summary>
    public void SkipToLastNode()
    {
        if (!isRunning)
        {
            Debug.LogWarning("[ScenarioRunner] 실행 중인 시나리오가 없습니다.");
            return;
        }

        if (!TryFindSkipTargetNode(out ScenarioData target))
        {
            Debug.LogWarning("[ScenarioRunner] 스킵할 노드를 찾을 수 없습니다.");
            return;
        }

        Debug.Log($"[ScenarioRunner] 디버그 스킵 → Id={target.id}, Trigger={target.trigger}");
        PlayNode(target);
    }

    private bool TryFindSkipTargetNode(out ScenarioData target)
    {
        target = null;

        if (scenario?.nodes == null || scenario.nodes.Count == 0)
        {
            return false;
        }

        for (int i = scenario.nodes.Count - 1; i >= 0; i--)
        {
            ScenarioData node = scenario.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            if (node.trigger == DialogTrigger.End)
            {
                target = node;
                return true;
            }
        }

        for (int i = scenario.nodes.Count - 1; i >= 0; i--)
        {
            ScenarioData node = scenario.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            target = node;
            return true;
        }

        return false;
    }

    private void PlayNode(ScenarioData node)
    {
        if (node == null)
        {
            FinishScenario();
            return;
        }

        currentNode = node;
        waitingForChoicePanel = false;
        choicePanel?.Hide();
        ApplyBackground(node);
        ApplyCharacterPortrait(node);
        ApplyCharacterName(node);

        textDialog.ShowText(ResolveScenarioText(node.text));

        if (node.trigger == DialogTrigger.Enter)
        {
            enterInput?.Show();
        }
        else
        {
            enterInput?.Hide();
        }
    }

    private string ResolveScenarioText(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return rawText;
        }

        bool usesInput = rawText.Contains(InputToken);
        bool usesCbt = rawText.Contains(CbtToken);
        if (!usesInput && !usesCbt)
        {
            return rawText;
        }

        string resolved = rawText;
        if (usesInput)
        {
            resolved = resolved.Replace(InputToken, lastEnterInput ?? string.Empty);
        }

        if (usesCbt)
        {
            resolved = resolved.Replace(CbtToken, lastCbtInput ?? string.Empty);
        }

        if (logEnterEchoDebug)
        {
            Debug.Log(
                $"[ScenarioRunner][EnterEcho] Text 치환\n" +
                $"- NodeId: {currentNode?.id}\n" +
                $"- {InputToken}: \"{lastEnterInput}\"\n" +
                $"- {CbtToken}: \"{lastCbtInput}\"\n" +
                $"- Result: \"{resolved}\"");
        }

        return resolved;
    }

    private void OnDialogClicked(string _)
    {
        if (!isRunning || currentNode == null)
        {
            return;
        }

        if (currentNode.trigger == DialogTrigger.Choice3)
        {
            if (!waitingForChoicePanel)
            {
                waitingForChoicePanel = true;
                choicePanel?.Show(currentNode);
            }

            return;
        }

        if (currentNode.trigger == DialogTrigger.Enter || isProcessingEnter)
        {
            return;
        }

        if (currentNode.trigger == DialogTrigger.End)
        {
            FinishScenario();
            return;
        }

        GoToNode(currentNode.nextId);
    }

    private async void OnEnterSubmitted(string userInput)
    {
        if (!isRunning || currentNode == null || currentNode.trigger != DialogTrigger.Enter || isProcessingEnter)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(userInput))
        {
            return;
        }

        isProcessingEnter = true;
        enterInput?.SetInteractable(false);

        string question = currentNode.text;
        string nextId = currentNode.nextId;
        string therapySituation = currentNode.ResolveTherapySituation();
        string rawInput = userInput.Trim();
        lastEnterInput = rawInput;
        lastCbtInput = string.Empty;

        if (logEnterEchoDebug)
        {
            Debug.Log(
                $"[ScenarioRunner][EnterEcho] 입력 제출\n" +
                $"- NodeId: {currentNode.id}\n" +
                $"- NextId: {nextId}\n" +
                $"- UseLocalLMEcho: {useLocalLMEcho}\n" +
                $"- UseLocalLMCbt: {useLocalLMCbt}\n" +
                $"- Situation: \"{therapySituation}\"\n" +
                $"- RawInput: \"{rawInput}\"");
        }

        LocalLLMService llmService = LocalLLMService.Instance;
        if (useLocalLMEcho || useLocalLMCbt)
        {
            if (llmService == null)
            {
                if (logEnterEchoDebug)
                {
                    Debug.LogWarning(
                        "[ScenarioRunner][EnterEcho] LocalLLMService가 씬에 없습니다. " +
                        "Tools > Local LLM > Setup Scene Service 실행 후 재시도하세요.");
                }
            }
            else
            {
                if (logEnterEchoDebug)
                {
                    Debug.Log(
                        $"[ScenarioRunner][EnterEcho] LLM 요청 시작\n" +
                        $"- IsReady: {llmService.IsReady}\n" +
                        $"- IsInitializing: {llmService.IsInitializing}");
                }

                if (useLocalLMEcho)
                {
                    string echoResult = await llmService.GenerateEchoAsync(
                        question,
                        rawInput,
                        therapySituation,
                        logEnterEchoDebug);
                    lastEnterInput = echoResult;

                    if (logEnterEchoDebug)
                    {
                        Debug.Log(
                            $"[ScenarioRunner][EnterEcho] 되비춤 적용\n" +
                            $"- Echo: \"{lastEnterInput}\"\n" +
                            $"- Raw와 동일: {string.Equals(rawInput, lastEnterInput, StringComparison.Ordinal)}");
                    }
                }
                else if (logEnterEchoDebug)
                {
                    Debug.Log("[ScenarioRunner][EnterEcho] UseLocalLMEcho=OFF, 원문을 {input}에 사용합니다.");
                }

                if (useLocalLMCbt)
                {
                    string cbtResult = await llmService.GenerateCbtAsync(
                        question,
                        rawInput,
                        lastEnterInput,
                        therapySituation,
                        logEnterEchoDebug);
                    lastCbtInput = cbtResult;

                    if (logEnterEchoDebug)
                    {
                        Debug.Log($"[ScenarioRunner][EnterEcho] CBT 대안 적용\n- Cbt: \"{lastCbtInput}\"");
                    }
                }
                else if (logEnterEchoDebug)
                {
                    Debug.Log("[ScenarioRunner][EnterEcho] UseLocalLMCbt=OFF, {cbt}는 비워 둡니다.");
                }
            }
        }
        else if (logEnterEchoDebug)
        {
            Debug.Log("[ScenarioRunner][EnterEcho] UseLocalLMEcho=OFF, UseLocalLMCbt=OFF");
        }

        isProcessingEnter = false;
        enterInput?.Hide();
        GoToNode(nextId);
    }

    private void OnChoiceSelected(int choiceIndex)
    {
        if (!isRunning || currentNode == null)
        {
            return;
        }

        string nextId = UI_ChoicePanel.GetNextId(currentNode, choiceIndex);
        GoToNode(nextId);
    }

    private void GoToNode(string nextId)
    {
        if (string.IsNullOrWhiteSpace(nextId))
        {
            FinishScenario();
            return;
        }

        if (!nodeById.TryGetValue(nextId.Trim(), out ScenarioData nextNode))
        {
            Debug.LogWarning($"[ScenarioRunner] Id '{nextId}' 노드를 찾을 수 없습니다.");
            FinishScenario();
            return;
        }

        PlayNode(nextNode);
    }

    private void FinishScenario()
    {
        isRunning = false;
        waitingForChoicePanel = false;
        isProcessingEnter = false;
        lastEnterInput = string.Empty;
        lastCbtInput = string.Empty;
        choicePanel?.Hide();
        enterInput?.Hide();
        textDialog?.Hide();
        HideCharacterName();
        OnScenarioEnded?.Invoke();
    }

    private void ApplyBackground(ScenarioData node)
    {
        if (backgroundImage == null || node == null || node.backgroundType == BackgroundType.None)
        {
            return;
        }

        ImageManager manager = ImageManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ScenarioRunner] ImageManager가 없습니다.");
            return;
        }

        if (!manager.TryApplyBackground(backgroundImage, node.backgroundType))
        {
            Debug.LogWarning($"[ScenarioRunner] 배경을 찾을 수 없습니다: {node.backgroundType}");
        }
    }

    private void ApplyCharacterPortrait(ScenarioData node)
    {
        if (characterImage == null || node == null)
        {
            return;
        }

        if (!ImageManager.ShouldShowPortrait(node.characterType))
        {
            characterImage.gameObject.SetActive(false);
            return;
        }

        ImageManager manager = ImageManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ScenarioRunner] ImageManager가 없습니다.");
            characterImage.gameObject.SetActive(false);
            return;
        }

        if (manager.TryGetCharacterImage(node.characterType, node.motionType, out Sprite sprite))
        {
            characterImage.gameObject.SetActive(true);
            characterImage.sprite = sprite;
            characterImage.enabled = true;
            return;
        }

        Debug.LogWarning(
            $"[ScenarioRunner] 초상을 찾을 수 없습니다: {node.characterType} / {node.motionType}");
        characterImage.gameObject.SetActive(false);
    }

    private void ApplyCharacterName(ScenarioData node)
    {
        if (nameObject == null || node == null)
        {
            return;
        }

        if (!CharacterDisplay.ShouldShowName(node.characterType))
        {
            nameObject.SetActive(false);
            return;
        }

        if (nameText != null)
        {
            nameText.text = CharacterDisplay.GetDisplayName(node.characterType);
        }

        nameObject.SetActive(true);
    }

    private void HideCharacterName()
    {
        if (nameObject != null)
        {
            nameObject.SetActive(false);
        }
    }

    private void BuildNodeLookup()
    {
        nodeById.Clear();

        if (scenario?.nodes == null)
        {
            return;
        }

        for (int i = 0; i < scenario.nodes.Count; i++)
        {
            ScenarioData node = scenario.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            string key = node.id.Trim();
            if (nodeById.ContainsKey(key))
            {
                Debug.LogWarning($"[ScenarioRunner] 중복 Id: {key}");
                continue;
            }

            nodeById.Add(key, node);
        }
    }

    private void SubscribeUiEvents()
    {
        textDialog.OnTextBalloonClicked += OnDialogClicked;

        if (choicePanel != null)
        {
            choicePanel.OnChoiceSelected += OnChoiceSelected;
        }

        if (enterInput != null)
        {
            enterInput.OnSubmitted += OnEnterSubmitted;
        }
    }

    private void UnsubscribeUiEvents()
    {
        if (textDialog != null)
        {
            textDialog.OnTextBalloonClicked -= OnDialogClicked;
        }

        if (choicePanel != null)
        {
            choicePanel.OnChoiceSelected -= OnChoiceSelected;
        }

        if (enterInput != null)
        {
            enterInput.OnSubmitted -= OnEnterSubmitted;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeUiEvents();
        OnScenarioEnded = null;
    }
}
