using UnityEngine;

public class BgmManager : MonoBehaviour
{
    [SerializeField]
    private AudioSource ac;

    [SerializeField]
    private AudioClip bgm;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        ac.clip = bgm;
        ac.loop = true;

        ac.Play();
    }
}