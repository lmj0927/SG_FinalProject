using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵 이동 UI. 현재 위치에 따라 Left/Right/Up 버튼 표시 및 목적지 라벨 갱신.
/// </summary>
public class UI_MoveMap : MonoBehaviour
{
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button upButton;
    [SerializeField] private TextMeshProUGUI leftLabel;
    [SerializeField] private TextMeshProUGUI rightLabel;
    [SerializeField] private TextMeshProUGUI upLabel;

    public event Action<MapDirection> OnDirectionClicked;

    private void Awake()
    {
        BindButton(leftButton, MapDirection.Left);
        BindButton(rightButton, MapDirection.Right);
        BindButton(upButton, MapDirection.Up);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Refresh(BackgroundType location)
    {
        RefreshDirection(MapDirection.Left, leftButton, leftLabel, location);
        RefreshDirection(MapDirection.Right, rightButton, rightLabel, location);
        RefreshDirection(MapDirection.Up, upButton, upLabel, location);
    }

    private static void RefreshDirection(
        MapDirection direction,
        Button button,
        TextMeshProUGUI label,
        BackgroundType location)
    {
        if (button == null)
        {
            return;
        }

        MapExit exit = MapConnectionTable.GetExit(location, direction);
        bool hasExit = exit.Target.HasValue;
        button.gameObject.SetActive(hasExit);

        if (hasExit && label != null)
        {
            label.text = exit.Label;
        }
    }

    private void BindButton(Button button, MapDirection direction)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnDirectionClicked?.Invoke(direction));
    }

    private void OnDestroy()
    {
        OnDirectionClicked = null;

        if (leftButton != null)
        {
            leftButton.onClick.RemoveAllListeners();
        }

        if (rightButton != null)
        {
            rightButton.onClick.RemoveAllListeners();
        }

        if (upButton != null)
        {
            upButton.onClick.RemoveAllListeners();
        }
    }
}
