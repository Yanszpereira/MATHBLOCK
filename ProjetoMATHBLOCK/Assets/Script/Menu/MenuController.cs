using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controla o menu principal e os menus das fases com fade em tempo real.
/// </summary>
public class MenuController : MonoBehaviour
{
    private bool gameplayPaused;
    private bool IsMainMenu => SceneManager.GetActiveScene().name == "MainMenu";

    [Header("Folha inicial")]
    [SerializeField] private Animation startPaper;

    [Header("Menus")]
    [SerializeField] private GameObject menuInicial;
    [SerializeField] private GameObject menuCreditos;
    [SerializeField] private GameObject menuOpcoes;
    [SerializeField] private GameObject menuSair;

    [Header("Blocker")]
    [SerializeField] private GameObject blocker;

    [Header("Transicao")]
    [SerializeField, Min(0f)] private float tempoAnimacao = 0.25f;
    [SerializeField] private AnimationCurve curvaFade =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private const string CaminhoCenaFase1 = "Assets/Scenes/Fase 1.unity";

    private readonly Dictionary<GameObject, Coroutine> rotinasAtivas = new();
    private readonly Dictionary<GameObject, CanvasGroup> canvasGroups = new();
    private readonly HashSet<Button> repairedButtons = new();
    private InputActionAsset runtimeUiActions;
    private InputAction menuBackAction;
    private DynamicCrosshair globalCrosshair;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneController()
    {
        if (FindFirstObjectByType<MenuController>(FindObjectsInactive.Include) != null)
            return;

        bool hasGameplayMenu = FindSceneObject("MenuPaper") != null;
        if (hasGameplayMenu)
            new GameObject("MenuController Runtime").AddComponent<MenuController>();
    }

    private void Awake()
    {
        if (!IsPreferredSceneController())
        {
            enabled = false;
            return;
        }

        RestaurarEscalaDaGameplayHud();
        ResolveMenuReferences();
        PrepararAcaoDeVoltar();
        RepararEventSystem();
        PrepararMenus();
        RepairAllHudButtonCalls();

        if (IsMainMenu)
        {
            Time.timeScale = 1f;
            DefinirCursor(true, false);
        }
    }

    private void PrepararAcaoDeVoltar()
    {
        if (IsMainMenu)
            return;

        // Uma acao propria continua recebendo o Esc mesmo quando o gameplay
        // esta pausado e nao depende do estado do mapa de acoes do Player/UI.
        menuBackAction = new InputAction("Pause Menu", InputActionType.Button);
        menuBackAction.AddBinding("<Keyboard>/escape");
        menuBackAction.AddBinding("<Gamepad>/start");
        menuBackAction.performed += AoPressionarVoltar;
        menuBackAction.Enable();
    }

    private void AoPressionarVoltar(InputAction.CallbackContext context)
    {
        OnBackButtonPressed();
    }
    private void RepararEventSystem()
    {
        InputSystemUIInputModule module =
            FindFirstObjectByType<InputSystemUIInputModule>(FindObjectsInactive.Include);

        if (module == null)
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            GameObject eventSystemObject = eventSystem != null
                ? eventSystem.gameObject
                : new GameObject("EventSystem", typeof(EventSystem));
            module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        // O asset que estava serializado na Fase 1 nao existe mais no projeto.
        // Reiniciamos o modulo para que ele registre os callbacks das novas acoes.
        module.enabled = false;

        runtimeUiActions = ScriptableObject.CreateInstance<InputActionAsset>();
        runtimeUiActions.name = "Runtime Menu UI Actions";
        InputActionMap ui = runtimeUiActions.AddActionMap("UI");

        InputAction point = ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
        point.AddBinding("<Mouse>/position");
        point.AddBinding("<Pen>/position");
        point.AddBinding("<Touchscreen>/touch*/position");

        InputAction click = ui.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
        click.AddBinding("<Mouse>/leftButton");
        click.AddBinding("<Pen>/tip");
        click.AddBinding("<Touchscreen>/touch*/press");

        InputAction rightClick = ui.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton");
        InputAction middleClick = ui.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton");
        InputAction scroll = ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");

        InputAction move = ui.AddAction("Move", InputActionType.PassThrough, expectedControlLayout: "Vector2");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick");
        move.AddBinding("<Gamepad>/dpad");

        InputAction submit = ui.AddAction("Submit", InputActionType.Button);
        submit.AddBinding("<Keyboard>/enter");
        submit.AddBinding("<Gamepad>/buttonSouth");

        InputAction cancel = ui.AddAction("Cancel", InputActionType.Button);
        cancel.AddBinding("<Gamepad>/buttonEast");

        module.actionsAsset = runtimeUiActions;
        module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
        module.point = InputActionReference.Create(point);
        module.leftClick = InputActionReference.Create(click);
        module.rightClick = InputActionReference.Create(rightClick);
        module.middleClick = InputActionReference.Create(middleClick);
        module.scrollWheel = InputActionReference.Create(scroll);
        module.move = InputActionReference.Create(move);
        module.submit = InputActionReference.Create(submit);
        module.cancel = InputActionReference.Create(cancel);

        runtimeUiActions.Enable();
        module.enabled = true;
    }

    private void PrepararMenus()
    {
        int sortingOrder = 2000;
        foreach (GameObject menu in TodosOsMenus())
        {
            if (menu == null)
                continue;

            bool manterAberto = IsMainMenu && menu == menuInicial && menu.activeSelf;

            if (!menu.TryGetComponent(out CanvasGroup group))
                group = menu.AddComponent<CanvasGroup>();

            group.alpha = manterAberto ? 1f : 0f;
            group.interactable = manterAberto;
            group.blocksRaycasts = manterAberto;
            canvasGroups[menu] = group;

            PrepararCamadaInterativa(menu, sortingOrder++);
            PrepararBotoes(menu);
            menu.SetActive(manterAberto);
        }

        if (blocker != null)
        {
            blocker.SetActive(IsMainMenu && EstaAberto(menuInicial));

            // O blocker serve para esconder/bloquear a HUD de gameplay, mas nao
            // pode ficar por cima dos botoes e capturar o ponteiro do menu.
            foreach (Graphic graphic in blocker.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

    private static void PrepararCamadaInterativa(GameObject menu, int sortingOrder)
    {
        Canvas canvas = menu.GetComponent<Canvas>();
        if (canvas == null)
            canvas = menu.AddComponent<Canvas>();

        // Canvas filho herda o modo de renderizacao, mas recebe prioridade sobre
        // HUD, mira, efeitos e imagens transparentes que estavam acima do menu.
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (menu.GetComponent<GraphicRaycaster>() == null)
            menu.AddComponent<GraphicRaycaster>();
    }

    private void PrepararBotoes(GameObject menu)
    {
        foreach (Button button in menu.GetComponentsInChildren<Button>(true))
        {
            button.interactable = true;

            // Os pais dos botoes usam RectTransform esticado. A hitbox deve ser
            // apenas o texto/icone visivel, senao todas as opcoes se sobrepoem.
            Graphic hitGraphic = null;
            foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                if (hitGraphic == null && graphic.transform != button.transform)
                    hitGraphic = graphic;
            }

            if (hitGraphic == null)
                hitGraphic = button.GetComponent<Graphic>();

            if (hitGraphic != null)
            {
                hitGraphic.raycastTarget = true;
                button.targetGraphic = hitGraphic;
            }

            foreach (HoverScale childHover in button.GetComponentsInChildren<HoverScale>(true))
                if (childHover.gameObject != button.gameObject)
                    childHover.enabled = false;

            if (button.GetComponent<HoverScale>() == null)
                button.gameObject.AddComponent<HoverScale>();

            RepairMissingPersistentCalls(button);
        }
    }

    private void ResolveMenuReferences()
    {
        globalCrosshair ??= GetComponentInParent<DynamicCrosshair>(true);
        menuInicial ??= FindSceneObject("MenuPaper");
        menuCreditos ??= FindSceneObject("CreditsMenu");
        menuOpcoes ??= FindSceneObject("OptionsMenu");
        menuSair ??= FindSceneObject("ExitMenu");
        blocker ??= FindSceneObject("Blocker");
    }

    private bool IsPreferredSceneController()
    {
        MenuController preferredHudController = null;
        foreach (MenuController candidate in FindObjectsByType<MenuController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.gameObject.scene == gameObject.scene &&
                IsInsideGameplayHud(candidate.transform))
            {
                preferredHudController = candidate;
                break;
            }
        }

        return preferredHudController == null || preferredHudController == this;
    }

    private static bool IsInsideGameplayHud(Transform target)
    {
        while (target != null)
        {
            if (target.name.Equals("Gameplay HUD", System.StringComparison.OrdinalIgnoreCase))
                return true;
            target = target.parent;
        }
        return false;
    }

    private void RestaurarEscalaDaGameplayHud()
    {
        if (!IsInsideGameplayHud(transform))
            return;

        Transform hudRoot = transform;
        while (hudRoot.parent != null && !hudRoot.name.Equals(
                   "Gameplay HUD",
                   System.StringComparison.OrdinalIgnoreCase))
        {
            hudRoot = hudRoot.parent;
        }

        // Uma instancia da Fase 1 foi salva com escala zero ao ser movida para
        // o Canvas. Isso ocultava tanto a HUD quanto os menus proprios da fase.
        if (hudRoot.localScale.sqrMagnitude < 0.0001f)
            hudRoot.localScale = Vector3.one;
    }

    private void RepairAllHudButtonCalls()
    {
        Transform hudRoot = transform;
        while (hudRoot.parent != null && !hudRoot.name.Equals(
                   "Gameplay HUD",
                   System.StringComparison.OrdinalIgnoreCase))
        {
            hudRoot = hudRoot.parent;
        }

        foreach (Button button in hudRoot.GetComponentsInChildren<Button>(true))
            RepairMissingPersistentCalls(button);
    }

    private static GameObject FindSceneObject(string expectedName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() &&
                candidate.name.Equals(expectedName, System.StringComparison.OrdinalIgnoreCase))
                return candidate.gameObject;
        }

        return null;
    }

    private void RepairMissingPersistentCalls(Button button)
    {
        if (button == null || !repairedButtons.Add(button))
            return;

        int persistentCount = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistentCount; i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null)
                continue;

            switch (button.onClick.GetPersistentMethodName(i))
            {
                case nameof(AbrirInicial): button.onClick.AddListener(AbrirInicial); break;
                case nameof(AbrirCreditos): button.onClick.AddListener(AbrirCreditos); break;
                case nameof(AbrirOpcoes): button.onClick.AddListener(AbrirOpcoes); break;
                case nameof(AbrirSair): button.onClick.AddListener(AbrirSair); break;
                case nameof(FecharInicial): button.onClick.AddListener(FecharInicial); break;
                case nameof(FecharCreditos): button.onClick.AddListener(FecharCreditos); break;
                case nameof(FecharOpcoes): button.onClick.AddListener(FecharOpcoes); break;
                case nameof(FecharSair): button.onClick.AddListener(FecharSair); break;
                case nameof(VoltarJogo): button.onClick.AddListener(VoltarJogo); break;
                case nameof(IniciarJogo): button.onClick.AddListener(IniciarJogo); break;
                case nameof(ReiniciarFase): button.onClick.AddListener(ReiniciarFase); break;
                case nameof(VoltarMenuPrincipal): button.onClick.AddListener(VoltarMenuPrincipal); break;
                case nameof(FecharJogo): button.onClick.AddListener(FecharJogo); break;
            }
        }
    }

    private IEnumerable<GameObject> TodosOsMenus()
    {
        yield return menuInicial;
        yield return menuCreditos;
        yield return menuOpcoes;
        yield return menuSair;
    }

    private void LateUpdate()
    {
        // Outros componentes do player podem tentar travar o cursor no mesmo
        // frame. Enquanto o menu estiver aberto, o menu sempre tem prioridade.
        if (gameplayPaused && !Application.isMobilePlatform)
            DefinirCursor(true, false);
    }

    public void OnBackButtonPressed()
    {
        if (!AlgumMenuAberto())
        {
            AbrirInicial();
            return;
        }

        if (EstaAberto(menuCreditos) || EstaAberto(menuOpcoes) || EstaAberto(menuSair))
        {
            FecharTodosOsSubmenus();
            AbrirInicial();
            return;
        }

        FecharInicial();
    }

    public void AbrirInicial() => AbrirMenu(menuInicial);
    public void AbrirCreditos() => AbrirMenu(menuCreditos);
    public void AbrirOpcoes() => AbrirMenu(menuOpcoes);
    public void AbrirSair() => AbrirMenu(menuSair);

    public void FecharInicial() => FecharMenu(menuInicial);
    public void FecharCreditos() => FecharMenu(menuCreditos);
    public void FecharOpcoes() => FecharMenu(menuOpcoes);
    public void FecharSair() => FecharMenu(menuSair);

    private void AbrirMenu(GameObject menu)
    {
        if (menu == null)
            return;

        PausarJogo();
        IniciarRotina(menu, Fade(menu, true));
    }

    private void FecharMenu(GameObject menu)
    {
        if (menu == null || !canvasGroups.ContainsKey(menu))
            return;

        IniciarRotina(menu, Fade(menu, false));
    }

    private void IniciarRotina(GameObject menu, IEnumerator rotina)
    {
        if (rotinasAtivas.TryGetValue(menu, out Coroutine atual) && atual != null)
            StopCoroutine(atual);

        rotinasAtivas[menu] = StartCoroutine(rotina);
    }

    private IEnumerator Fade(GameObject menu, bool abrir)
    {
        CanvasGroup group = canvasGroups[menu];

        if (abrir)
        {
            menu.SetActive(true);
            menu.transform.SetAsLastSibling();
            TocarPapel("OpenPaper");
            if (blocker != null)
                blocker.SetActive(true);
        }
        else
        {
            TocarPapel("ClosePaper");
        }

        group.interactable = false;
        group.blocksRaycasts = abrir;

        float inicio = group.alpha;
        float fim = abrir ? 1f : 0f;
        // Cenas antigas serializaram zero; ainda fazemos um fade curto e estavel.
        float duracao = tempoAnimacao > 0f ? tempoAnimacao : 0.18f;
        float elapsed = 0f;

        while (elapsed < duracao)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duracao);
            group.alpha = Mathf.Lerp(inicio, fim, curvaFade.Evaluate(normalized));
            yield return null;
        }

        group.alpha = fim;
        group.interactable = abrir;
        group.blocksRaycasts = abrir;

        if (!abrir)
            menu.SetActive(false);

        rotinasAtivas[menu] = null;
        AtualizarEstadoDoMenu();
    }

    private void TocarPapel(string clipName)
    {
        if (!IsMainMenu || startPaper == null)
            return;

        AnimationClip clip = startPaper.GetClip(clipName);
        if (clip == null)
            return;

        startPaper[clip.name].time = 0f;
        startPaper.Play(clip.name);
        startPaper.Sample();
    }

    public void VoltarJogo()
    {
        FecharTodosOsMenusImediatamente();
        RetomarJogo();
    }

    public void IniciarJogo()
    {
        Time.timeScale = 1f;
        int buildIndex = SceneUtility.GetBuildIndexByScenePath("Fase 1");
        if (buildIndex < 0)
        {
            Debug.LogError(
                $"Cena '{"Fase 1"}' nao foi adicionada ao Build Settings ativo.",
                this);
            return;
        }

        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }

    public void ReiniciarFase()
    {
        RetomarJogo();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VoltarMenuPrincipal()
    {
        RetomarJogo();
        SceneManager.LoadScene("MainMenu");
    }

    public void FecharJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PausarJogo()
    {
        if (IsMainMenu)
            return;

        gameplayPaused = true;
        Time.timeScale = 0f;
        globalCrosshair?.SetVisible(false);
        DefinirCursor(true, false);
    }

    private void RetomarJogo()
    {
        if (IsMainMenu)
            return;

        gameplayPaused = false;
        Time.timeScale = 1f;
        globalCrosshair?.SetVisible(true);
        if (!Application.isMobilePlatform)
            DefinirCursor(false, true);
    }

    private static void DefinirCursor(bool visivel, bool travado)
    {
        if (Application.isMobilePlatform)
            return;

        Cursor.visible = visivel;
        Cursor.lockState = travado ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private static bool EstaAberto(GameObject menu) => menu != null && menu.activeSelf;

    private bool AlgumMenuAberto() =>
        EstaAberto(menuInicial) || EstaAberto(menuCreditos) ||
        EstaAberto(menuOpcoes) || EstaAberto(menuSair);

    private void AtualizarEstadoDoMenu()
    {
        bool aberto = AlgumMenuAberto();
        if (blocker != null)
            blocker.SetActive(aberto);
        if (!aberto)
            RetomarJogo();
    }

    private void FecharTodosOsSubmenus()
    {
        FecharMenu(menuCreditos);
        FecharMenu(menuOpcoes);
        FecharMenu(menuSair);
    }

    private void FecharTodosOsMenusImediatamente()
    {
        foreach (GameObject menu in TodosOsMenus())
        {
            if (menu == null)
                continue;
            if (rotinasAtivas.TryGetValue(menu, out Coroutine rotina) && rotina != null)
                StopCoroutine(rotina);
            menu.SetActive(false);
        }

        if (blocker != null)
            blocker.SetActive(false);
    }

    private void OnDestroy()
    {
        if (gameplayPaused)
            Time.timeScale = 1f;

        if (menuBackAction != null)
        {
            menuBackAction.performed -= AoPressionarVoltar;
            menuBackAction.Disable();
            menuBackAction.Dispose();
        }
        if (runtimeUiActions != null)
        {
            runtimeUiActions.Disable();
            Destroy(runtimeUiActions);
        }
    }
}
