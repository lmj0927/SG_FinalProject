using System;
using System.Collections.Generic;
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
    [SerializeField] private Image characterImage;

    [Header("Behavior")]
    [SerializeField] private bool playOnStart = true;

    private readonly Dictionary<string, ScenarioData> nodeById = new Dictionary<string, ScenarioData>();

    private ScenarioData currentNode;
    private bool isRunning;
    private bool waitingForChoicePanel;

    public event Action OnScenarioEnded;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        BuildNodeLookup();
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartScenario();
        }
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
        choicePanel?.Hide();

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
        ApplyCharacterPortrait(node);

        textDialog.ShowText(node.text);
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

        if (currentNode.trigger == DialogTrigger.End)
        {
            FinishScenario();
            return;
        }

        GoToNode(currentNode.nextId);
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
        choicePanel?.Hide();
        textDialog?.Hide();
        OnScenarioEnded?.Invoke();
    }

    private void ApplyCharacterPortrait(ScenarioData node)
    {
        if (characterImage == null || node == null)
        {
            return;
        }

        if (scenario == null || string.IsNullOrWhiteSpace(scenario.characterId))
        {
            characterImage.enabled = false;
            return;
        }

        CharacterImageManager manager = CharacterImageManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ScenarioRunner] CharacterImageManager가 없습니다.");
            characterImage.enabled = false;
            return;
        }

        string characterId = scenario.characterId.Trim();
        if (manager.TryGetCharacterImage(characterId, node.motionType, out Sprite sprite))
        {
            characterImage.gameObject.SetActive(true);
            characterImage.sprite = sprite;
            characterImage.enabled = true;
            return;
        }

        Debug.LogWarning(
            $"[ScenarioRunner] 초상을 찾을 수 없습니다: {characterId} / {node.motionType}");
        characterImage.enabled = false;
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
    }

    private void OnDestroy()
    {
        UnsubscribeUiEvents();
        OnScenarioEnded = null;
    }
}
