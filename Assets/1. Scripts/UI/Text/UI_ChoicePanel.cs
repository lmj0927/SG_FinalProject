using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3지선다 선택 UI. 버튼 텍스트 설정, 클릭 이벤트, 선택 시 Hide.
/// </summary>
public class UI_ChoicePanel : MonoBehaviour
{
    [Header("Choice Buttons (Canvas/Buttons 하위 3개)")]
    [SerializeField] private Button choiceButton1;
    [SerializeField] private Button choiceButton2;
    [SerializeField] private Button choiceButton3;

    [Header("Labels (비워두면 Button 자식 TMP 자동 연결)")]
    [SerializeField] private TextMeshProUGUI choiceLabel1;
    [SerializeField] private TextMeshProUGUI choiceLabel2;
    [SerializeField] private TextMeshProUGUI choiceLabel3;

    [Header("Panel")]
    [SerializeField] private RectTransform panelRectTransform;

    [Header("Show Animation")]
    [SerializeField] private bool showUseScale = true;
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private Ease appearEase = Ease.OutCubic;

    [Header("Hide Animation")]
    [SerializeField] private bool hideUseScale = true;
    [SerializeField] private float disappearDuration = 0.3f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    public event Action<int> OnChoiceSelected;

    private Button[] buttons;
    private TextMeshProUGUI[] labels;
    private bool isInitialized;
    private bool isVisible;
    private bool isHiding;
    private Tween panelTween;

    private void Awake()
    {
        EnsureInitialized();
        ApplyHiddenState();
    }

    public void ShowChoices(string label1, string label2, string label3)
    {
        EnsureInitialized();

        ApplyChoice(0, label1);
        ApplyChoice(1, label2);
        ApplyChoice(2, label3);

        KillPanelTween();
        isHiding = false;
        isVisible = true;
        gameObject.SetActive(true);
        ApplyShowStartState();
        SetButtonsInteractable(false);

        if (!TryPlayShowAnimation())
        {
            CompleteShow();
        }
    }

    public void Show(ScenarioData node)
    {
        if (node == null)
        {
            Debug.LogWarning("[UI_ChoicePanel] ScenarioData가 null입니다.");
            return;
        }

        ShowChoices(node.c1Label, node.c2Label, node.c3Label);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        if (isHiding)
        {
            return;
        }

        KillPanelTween();
        isVisible = false;
        isHiding = true;
        SetButtonsInteractable(false);

        if (!TryPlayHideAnimation())
        {
            CompleteHide();
        }
    }

    public static string GetNextId(ScenarioData node, int choiceIndex)
    {
        if (node == null)
        {
            return string.Empty;
        }

        switch (choiceIndex)
        {
            case 0: return node.c1NextId;
            case 1: return node.c2NextId;
            case 2: return node.c3NextId;
            default: return string.Empty;
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        if (panelRectTransform == null)
        {
            panelRectTransform = transform as RectTransform;
        }

        buttons = new[] { choiceButton1, choiceButton2, choiceButton3 };
        labels = new[] { choiceLabel1, choiceLabel2, choiceLabel3 };

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            if (labels[i] == null)
            {
                labels[i] = buttons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            }

            int index = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => HandleChoiceClicked(index));
        }

        isInitialized = true;
    }

    private void ApplyChoice(int index, string label)
    {
        EnsureInitialized();

        if (buttons == null || index < 0 || index >= buttons.Length || buttons[index] == null)
        {
            return;
        }

        bool hasLabel = !string.IsNullOrWhiteSpace(label);
        buttons[index].gameObject.SetActive(hasLabel);

        if (hasLabel && labels[index] != null)
        {
            labels[index].text = label;
        }
    }

    private void ApplyShowStartState()
    {
        if (panelRectTransform != null)
        {
            panelRectTransform.localScale = showUseScale ? Vector3.zero : Vector3.one;
        }
    }

    private void ApplyHiddenState()
    {
        if (panelRectTransform != null && showUseScale)
        {
            panelRectTransform.localScale = Vector3.zero;
        }

        SetButtonsInteractable(false);
    }

    private bool TryPlayShowAnimation()
    {
        if (!showUseScale || panelRectTransform == null)
        {
            return false;
        }

        panelTween = panelRectTransform
            .DOScale(Vector3.one, appearDuration)
            .SetEase(appearEase)
            .SetUpdate(true)
            .OnComplete(CompleteShow);

        return true;
    }

    private bool TryPlayHideAnimation()
    {
        if (!hideUseScale || panelRectTransform == null)
        {
            return false;
        }

        panelTween = panelRectTransform
            .DOScale(Vector3.zero, disappearDuration)
            .SetEase(disappearEase)
            .SetUpdate(true)
            .OnComplete(CompleteHide);

        return true;
    }

    private void CompleteShow()
    {
        if (!isVisible)
        {
            return;
        }

        SetButtonsInteractable(true);
    }

    private void CompleteHide()
    {
        isHiding = false;
        ApplyHiddenState();
        gameObject.SetActive(false);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].interactable = interactable && buttons[i].gameObject.activeSelf;
            }
        }
    }

    private void KillPanelTween()
    {
        if (panelTween != null && panelTween.IsActive())
        {
            panelTween.Kill();
        }

        panelTween = null;
    }

    private void HandleChoiceClicked(int choiceIndex)
    {
        OnChoiceSelected?.Invoke(choiceIndex);
        Hide();
    }

    private void OnDestroy()
    {
        KillPanelTween();
        OnChoiceSelected = null;

        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].onClick.RemoveAllListeners();
            }
        }
    }
}
