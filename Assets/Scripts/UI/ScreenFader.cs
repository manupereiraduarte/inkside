using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Va en el FadeImage del Canvas de cada escena. Al arrancar la escena desvanece la pantalla
/// de negro (alpha 1 → 0); FadeOutThenLoad funde a negro y carga la escena pedida.
/// Cada escena trae su propio fader: no persiste entre escenas.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [SerializeField] CanvasGroup fadeGroup;
    [SerializeField] float fadeDuration = 1f;

    bool _loading;

    /// <summary>true desde que arranca el fundido de salida hasta que se carga la escena siguiente.</summary>
    public bool IsLoading => _loading;

    void Awake()
    {
        if (fadeGroup == null) fadeGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        if (fadeGroup == null)
        {
            Debug.LogWarning("[ScreenFader] Sin CanvasGroup asignado; no hay fundido.", this);
            return;
        }
        fadeGroup.alpha = 1f;
        StartCoroutine(Fade(1f, 0f, null));
    }

    public void FadeOutThenLoad(string sceneName)
    {
        if (_loading) return;
        _loading = true;

        if (fadeGroup == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(Fade(fadeGroup.alpha, 1f, sceneName));
    }

    IEnumerator Fade(float from, float to, string sceneToLoad)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        fadeGroup.alpha = to;

        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
    }
}
