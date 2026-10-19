using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Objeto objetivo de cada mundo (Anillo / Rosa / Snitch): al interactuar suena el SFX de recolección,
/// se oculta el visual y se vuelve a la Biblioteca con fundido a negro.
/// </summary>
public class ObjectTargetReturn : MonoBehaviour, IInteractable
{
    [SerializeField] AudioClip collectSfx;
    [SerializeField] GameObject visualToHide;
    [SerializeField] ScreenFader fader;
    [SerializeField] string hubSceneName = "00_Biblioteca";

    bool _used;

    public string GetInteractionPrompt() => "[E] Tomar";

    public void Interact()
    {
        if (_used) return;
        _used = true;

        PlayThroughSceneLoad(collectSfx);

        if (visualToHide != null)
            visualToHide.SetActive(false);

        if (fader != null)
            fader.FadeOutThenLoad(hubSceneName);
        else
            SceneManager.LoadScene(hubSceneName);
    }

    /// <summary>
    /// Reproduce el clip en un objeto que sobrevive al cambio de escena. Con AudioSource.PlayClipAtPoint
    /// el objeto temporal vive en la escena actual y LoadScene lo destruye, cortando el sonido a mitad.
    /// </summary>
    static void PlayThroughSceneLoad(AudioClip clip)
    {
        if (clip == null) return;
        var go = new GameObject("SFX_" + clip.name);
        DontDestroyOnLoad(go);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 0f;
        source.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
