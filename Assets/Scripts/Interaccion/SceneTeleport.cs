using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Libro-portal de la Biblioteca: al interactuar reproduce el SFX de pasar página y
/// pide al <see cref="ScreenFader"/> de la escena que funda a negro y cargue el mundo destino.
/// </summary>
public class SceneTeleport : MonoBehaviour, IInteractable
{
    [SerializeField] string targetSceneName;
    [SerializeField] AudioClip pageTurnSfx;
    [SerializeField] ScreenFader fader;

    bool _used;

    public string GetInteractionPrompt() => "[E] Abrir el libro";

    public void Interact()
    {
        if (_used) return;
        _used = true;

        PlayThroughSceneLoad(pageTurnSfx);

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError($"[SceneTeleport] '{name}' no tiene targetSceneName asignado.", this);
            _used = false;
            return;
        }

        if (fader != null)
            fader.FadeOutThenLoad(targetSceneName);
        else
            SceneManager.LoadScene(targetSceneName);
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
