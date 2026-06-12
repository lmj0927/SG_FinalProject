using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 맵 이동. ScenarioManager(미구현) 등에서 EnterMapMode/ExitMapMode로 제어합니다.
/// </summary>
public class MapNavigator : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private UI_MoveMap moveMapUI;

    public BackgroundType CurrentLocation { get; private set; } = BackgroundType.None;
    public bool IsMapModeActive { get; private set; }

    public event Action<BackgroundType> OnLocationChanged;

    private void Awake()
    {
        if (moveMapUI != null)
        {
            moveMapUI.OnDirectionClicked += HandleDirectionClicked;
        }
    }

    private void Start()
    {
        moveMapUI?.Hide();
    }

    /// <summary>
    /// 맵 탐색 모드 진입. startLocation에서 시작합니다.
    /// </summary>
    public void EnterMapMode(BackgroundType startLocation)
    {
        if (!MapConnectionTable.IsNavigableMap(startLocation))
        {
            Debug.LogWarning($"[MapNavigator] 맵 이동 불가 위치: {startLocation}");
            return;
        }

        if (moveMapUI == null)
        {
            Debug.LogWarning("[MapNavigator] moveMapUI가 연결되지 않았습니다.");
            return;
        }

        IsMapModeActive = true;
        moveMapUI.Show();
        SetLocation(startLocation);
        moveMapUI.Refresh(CurrentLocation);

        Debug.Log($"[MapNavigator] 맵 탐색 모드 진입: {startLocation}, MoveMap active={moveMapUI.gameObject.activeSelf}");
    }

    /// <summary>
    /// 맵 탐색 모드 종료. MoveMap UI만 숨깁니다.
    /// </summary>
    public void ExitMapMode()
    {
        IsMapModeActive = false;
        moveMapUI?.Hide();
    }

    private void HandleDirectionClicked(MapDirection direction)
    {
        if (!IsMapModeActive)
        {
            return;
        }

        MapExit exit = MapConnectionTable.GetExit(CurrentLocation, direction);
        if (!exit.Target.HasValue)
        {
            return;
        }

        SetLocation(exit.Target.Value);
        moveMapUI?.Refresh(CurrentLocation);
    }

    private void SetLocation(BackgroundType location)
    {
        if (!MapConnectionTable.IsNavigableMap(location))
        {
            Debug.LogWarning($"[MapNavigator] 유효하지 않은 맵: {location}");
            return;
        }

        CurrentLocation = location;
        ApplyBackground(location);
        OnLocationChanged?.Invoke(location);
    }

    private void ApplyBackground(BackgroundType location)
    {
        ImageManager manager = ImageManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[MapNavigator] ImageManager가 없습니다.");
            return;
        }

        if (!manager.TryApplyBackground(backgroundImage, location))
        {
            Debug.LogWarning($"[MapNavigator] 배경을 찾을 수 없습니다: {location}");
        }
    }

    private void OnDestroy()
    {
        if (moveMapUI != null)
        {
            moveMapUI.OnDirectionClicked -= HandleDirectionClicked;
        }

        OnLocationChanged = null;
    }
}
