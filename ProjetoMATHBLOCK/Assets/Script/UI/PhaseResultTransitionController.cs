using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PhaseResultTransitionController : MonoBehaviour
{
    private static PhaseResultTransitionController instance;

    [Header("Optional Inspector visuals")]
    [SerializeField] private Image paperBackground;
    [SerializeField] private Image gradeImageA;
    [SerializeField] private Image gradeImageB;
    [SerializeField] private Image gradeImageC;
    [SerializeField] private Image gradeImageD;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text gradeText;

    private CanvasGroup fadeGroup;
    private GameObject panel;
    private int choice;
    private bool busy;

    public static bool TryShowResult(string destinationScene, bool finalPhase, Object context = null)
    {
        if (string.IsNullOrWhiteSpace(destinationScene) ||
            !Application.CanStreamedLevelBeLoaded(destinationScene))
            return false;

        if (instance == null)
        {
            GameObject host = new GameObject("Phase Result Transition");
            instance = host.AddComponent<PhaseResultTransitionController>();
        }

        if (instance.busy)
            return false;

        instance.StartCoroutine(instance.ShowRoutine(destinationScene, finalPhase));
        return true;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureUi();
    }

private IEnumerator ShowRoutine(string destinationScene, bool finalPhase)
    {
        busy = true;
        choice = 0;
        fadeGroup.blocksRaycasts = true;
        Scene sourceScene = SceneManager.GetActiveScene();

        yield return FadeTo(1f, 0.6f);
        ConfigurePanel(finalPhase);
        panel.SetActive(true);

        while (choice == 0)
            yield return null;

        panel.SetActive(false);
        string targetScene = choice == 1 ? destinationScene : sourceScene.name;
        // A tela de notas tambem troca cenas; aplica o mesmo encerramento de
        // audio do fluxo principal antes que os emissores da proxima iniciem.
        SceneTransitionManager.PararAudioDaCenaAntesDeCarregar();
        AsyncOperation load = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        while (load != null && !load.isDone)
            yield return null;

        yield return null;
        yield return FadeTo(0f, 0.6f);
        fadeGroup.blocksRaycasts = false;
        busy = false;
    }

    private void ConfigurePanel(bool finalPhase)
    {
        string grade;
        if (finalPhase && ResultadosDasFases.TryObterResultadoFinal(out ResultadoFinal final))
        {
            messageText.text = "Parabéns, você finalizou o jogo! Esta é a tua nota final:";
            grade = final.Nota;
        }
        else
        {
            if (!ResultadosDasFases.TryObterResultado(GetCurrentPhase(), out ResultadoDaFase result))
                result = new ResultadoDaFase(GetCurrentPhase(), 0f, "D");

            messageText.text = "Parabéns, você completou a fase. Aqui está a sua nota:";
            grade = result.Nota;
        }

        gradeText.text = grade;
        SetGradeImage(grade);
        SetButtonLabels(finalPhase ? "Voltar ao menu principal" : "Ir para a próxima fase");
    }

    private int GetCurrentPhase()
    {
        string name = SceneManager.GetActiveScene().name;
        return name.StartsWith("Fase ") && int.TryParse(name.Substring(5), out int phase) ? phase : 0;
    }

    private void EnsureUi()
    {
        GameObject canvasObject = new GameObject("Phase Result Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;

        Image black = CreateImage("Black", canvasObject.transform, Color.black);
        Stretch(black.rectTransform);

        panel = new GameObject("Phase Result Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = paperBackground != null ? paperBackground.color : new Color(0.96f, 0.91f, 0.78f, 1f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(.5f, .5f);
        panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(920f, 520f);

        messageText ??= CreateText("Message", panel.transform, 34, TextAlignmentOptions.Center);
        SetRect(messageText.rectTransform, new Vector2(60, -55), new Vector2(-60, -175));

        gradeText ??= CreateText("Grade", panel.transform, 108, TextAlignmentOptions.Center);
        SetRect(gradeText.rectTransform, new Vector2(60, -185), new Vector2(-60, -325));

        CreateButton("Next Button", panel.transform, new Vector2(-390, -445), () => choice = 1);
        CreateButton("Restart Button", panel.transform, new Vector2(390, -445), () => choice = 2);
        panel.SetActive(false);

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void SetButtonLabels(string nextLabel)
    {
        TextMeshProUGUI[] labels = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.name == "Next Button Label") label.text = nextLabel;
            if (label.name == "Restart Button Label") label.text = "Reiniciar a fase";
        }
    }

    private void SetGradeImage(string grade)
    {
        if (gradeImageA != null) gradeImageA.gameObject.SetActive(grade == "A");
        if (gradeImageB != null) gradeImageB.gameObject.SetActive(grade == "B");
        if (gradeImageC != null) gradeImageC.gameObject.SetActive(grade == "C");
        if (gradeImageD != null) gradeImageD.gameObject.SetActive(grade == "D");
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = fadeGroup.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            fadeGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TMP_Text text = item.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = Color.black;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        return text;
    }

    private static void CreateButton(string name, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 90f);
        item.GetComponent<Image>().color = new Color(.18f, .35f, .75f, 1f);
        item.GetComponent<Button>().onClick.AddListener(action);

        TMP_Text label = CreateText(name + " Label", item.transform, 28, TextAlignmentOptions.Center);
        label.color = Color.white;
        Stretch(label.rectTransform);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}
