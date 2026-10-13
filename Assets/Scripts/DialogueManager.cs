using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays DialogueAssets in a bottom-of-screen box with a typewriter effect, choices,
/// speaker styling and optional camera focus. The UI is built at runtime, so no scene
/// setup is needed: DialogueManager.Instance creates itself on first use.
/// Freezes the player while a dialogue is open.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    private const int MaxEmptyHops = 32;

    private static DialogueManager instance;

    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<DialogueManager>();

            if (instance == null)
                instance = new GameObject("DialogueManager").AddComponent<DialogueManager>();

            return instance;
        }
    }

    /// <summary>True while a dialogue is open.</summary>
    public static bool IsActive => instance != null && instance.active;

    /// <summary>
    /// True while a dialogue is open and also during the frame it closed, so the key press
    /// that closed it cannot be re-used by another interaction in the same frame.
    /// </summary>
    public static bool IsBusy => instance != null && (instance.active || instance.endFrame == Time.frameCount);

    public static event Action OnDialogueStarted;
    public static event Action OnDialogueEnded;

    [Header("References")]
    [Tooltip("Frozen while talking. Found automatically if left empty.")]
    public SimpleFPSControllerNewInput playerController;

    [Header("Input")]
    public KeyCode advanceKey = KeyCode.E;
    public bool allowSpace = true;
    public bool allowMouseClick = true;

    [Header("Typewriter")]
    public float charactersPerSecond = 40f;

    [Header("Voice")]
    [Tooltip("Babble voice for lines without a speaker profile. A profile's own voice overrides it.")]
    public VoiceSettings defaultVoice = new VoiceSettings();

    [Header("Camera Focus")]
    [Tooltip("Seconds the view takes to turn towards the speaker.")]
    public float focusSmoothTime = 0.25f;

    [Header("Look")]
    public float panelHeight = 190f;
    public float panelBottomMargin = 50f;
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    public Color speakerColor = new Color(0.5f, 0.5f, 0.46f, 0.85f);
    public Color textColor = new Color(0.72f, 0.72f, 0.68f, 0.95f);
    public float speakerFontSize = 24f;
    public float bodyFontSize = 26f;
    public string continueText = "[E]";
    [Tooltip("Seconds to fade the dialogue boxes in and out.")]
    public float fadeTime = 0.3f;

    [Header("Choices")]
    public float choiceFontSize = 24f;
    public Color choiceColor = new Color(0.6f, 0.6f, 0.58f, 0.45f);
    public Color choiceSelectedColor = new Color(0.85f, 0.85f, 0.8f, 1f);

    [Header("Interact Prompt")]
    public float interactPromptFontSize = 22f;
    public float interactPromptOffsetY = -90f;

    private CanvasGroup fadeGroup;
    private GameObject panel;
    private TextMeshProUGUI speakerLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI continueLabel;
    private TextMeshProUGUI interactPromptLabel;
    private GameObject choicePanel;
    private readonly List<TextMeshProUGUI> choiceLabels = new List<TextMeshProUGUI>();
    private AudioSource audioSource;
    private DialogueVoice voice;

    private readonly List<DialogueChoice> activeChoices = new List<DialogueChoice>();
    private DialogueAsset currentAsset;
    private int lineIndex;
    private int emptyHops;
    private Action onFinished;
    private Action<DialogueAsset, int> onLineStarted;
    private Func<Vector3> focusProvider;

    private bool active;
    private bool typing;
    private bool choiceMode;
    private bool controlledPlayer;
    private int choiceIndex;
    private float visibleCharacters;
    private int totalCharacters;
    private int startFrame = -1;
    private int endFrame = -1;

    private float currentSpeed;
    private VoiceSettings currentVoice;
    private VoiceNote[] currentNotes;
    private int lastVoiceCount;

    private float yawVelocity;
    private float pitchVelocity;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Plays per-line voice clips (DialogueLine.voice). The babble voice has its own sources.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        voice = gameObject.AddComponent<DialogueVoice>();

        BuildUI();
        panel.SetActive(false);
        choicePanel.SetActive(false);
        interactPromptLabel.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Public API

    /// <summary>
    /// Starts a dialogue. Returns false if one is already running or the asset has nothing to play.
    /// focus: world position the camera turns towards (null = leave the camera alone).
    /// onFinished: invoked after the conversation ends (including after choices).
    /// onLineStarted: invoked for every line shown, with the asset it belongs to and its index.
    /// </summary>
    public bool StartDialogue(
        DialogueAsset asset,
        Func<Vector3> focus = null,
        Action onFinished = null,
        Action<DialogueAsset, int> onLineStarted = null)
    {
        if (active || asset == null || !asset.IsPlayable)
            return false;

        this.onFinished = onFinished;
        this.onLineStarted = onLineStarted;
        focusProvider = focus;
        yawVelocity = 0f;
        pitchVelocity = 0f;

        active = true;
        startFrame = Time.frameCount;

        if (playerController == null)
            playerController = FindFirstObjectByType<SimpleFPSControllerNewInput>();

        if (playerController != null && !playerController.isInspecting)
        {
            playerController.isInspecting = true;
            controlledPlayer = true;
        }

        interactPromptLabel.gameObject.SetActive(false);
        panel.SetActive(true);
        emptyHops = 0;
        BeginAsset(asset);

        // BeginAsset can end immediately (e.g. an asset with only empty links).
        if (active)
            OnDialogueStarted?.Invoke();

        return true;
    }

    /// <summary>Shows a prompt such as "[E] Talk" in the middle of the screen.</summary>
    public void ShowInteractPrompt(string text)
    {
        if (active)
            return;

        interactPromptLabel.text = text;
        interactPromptLabel.gameObject.SetActive(true);
    }

    public static void HidePrompt()
    {
        if (instance != null && instance.interactPromptLabel != null)
            instance.interactPromptLabel.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Flow

    private void Update()
    {
        UpdateFade();

        if (!active)
            return;

        // Ignore the key press that opened the dialogue.
        if (Time.frameCount == startFrame)
            return;

        bool pressed = Input.GetKeyDown(advanceKey)
                       || (allowSpace && Input.GetKeyDown(KeyCode.Space))
                       || (allowMouseClick && Input.GetMouseButtonDown(0));

        if (choiceMode)
        {
            UpdateChoices(pressed);
            return;
        }

        if (typing)
        {
            if (pressed)
            {
                // Skipping cuts the voice instead of letting the rest of the line babble on.
                voice.StopAll();
                FinishTyping();
                return;
            }

            AdvanceTypewriter();
        }
        else
        {
            if (pressed)
            {
                NextLine();
                return;
            }

            // Blink the continue prompt once the line is complete.
            Color c = continueLabel.color;
            c.a = Mathf.Lerp(0.1f, 0.5f, Mathf.PingPong(Time.unscaledTime * 0.8f, 1f));
            continueLabel.color = c;
        }
    }

    private void UpdateFade()
    {
        float target = active ? 1f : 0f;

        if (Mathf.Approximately(fadeGroup.alpha, target))
            return;

        float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
        fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, target, step);

        // Fully faded out: switch the boxes off.
        if (!active && fadeGroup.alpha <= 0f)
        {
            panel.SetActive(false);
            choicePanel.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!active || focusProvider == null || playerController == null)
            return;

        Transform cam = playerController.cameraTransform;
        if (cam == null)
            return;

        Vector3 direction = focusProvider() - cam.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 euler = Quaternion.LookRotation(direction).eulerAngles;
        float targetYaw = euler.y;
        float targetPitch = Mathf.Clamp(
            Mathf.DeltaAngle(0f, euler.x),
            -playerController.maxLookAngle,
            playerController.maxLookAngle);

        Transform body = playerController.transform;
        float yaw = Mathf.SmoothDampAngle(body.eulerAngles.y, targetYaw, ref yawVelocity, focusSmoothTime);
        body.rotation = Quaternion.Euler(0f, yaw, 0f);

        float pitch = Mathf.SmoothDamp(playerController.Pitch, targetPitch, ref pitchVelocity, focusSmoothTime);
        playerController.Pitch = pitch;
        cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private void BeginAsset(DialogueAsset asset)
    {
        currentAsset = asset;
        lineIndex = 0;
        DialogueState.MarkPlayed(asset);

        if (asset.HasLines)
        {
            emptyHops = 0;
            ShowLine(asset.lines[0]);
            return;
        }

        if (asset.HasChoices)
        {
            ShowChoices();
            return;
        }

        if (asset.next != null && ++emptyHops < MaxEmptyHops)
        {
            BeginAsset(asset.next);
            return;
        }

        EndDialogue();
    }

    private void ShowLine(DialogueLine line)
    {
        HideChoices();

        SpeakerProfile profile = line.profile;

        string speakerName = !string.IsNullOrEmpty(line.speaker)
            ? line.speaker
            : (profile != null ? profile.displayName : null);

        bool hasSpeaker = !string.IsNullOrEmpty(speakerName);
        speakerLabel.gameObject.SetActive(hasSpeaker);
        speakerLabel.text = hasSpeaker ? speakerName : string.Empty;
        speakerLabel.color = profile != null ? profile.nameColor : speakerColor;
        bodyLabel.color = profile != null ? profile.textColor : textColor;

        currentVoice = profile != null && profile.voice != null ? profile.voice : defaultVoice;
        currentSpeed = charactersPerSecond * (profile != null ? Mathf.Max(0.01f, profile.speedMultiplier) : 1f);

        bodyLabel.text = line.text ?? string.Empty;
        bodyLabel.maxVisibleCharacters = 0;
        bodyLabel.ForceMeshUpdate();
        totalCharacters = bodyLabel.textInfo.characterCount;

        // Plan the whole line's melody now; the typewriter then just looks notes up by character index.
        // textInfo only holds visible characters, so rich-text tags are never voiced.
        char[] visible = new char[totalCharacters];
        for (int i = 0; i < totalCharacters; i++)
            visible[i] = bodyLabel.textInfo.characterInfo[i].character;

        string visibleText = new string(visible);
        currentNotes = VoiceContour.Build(visibleText, currentVoice, VoiceContour.Seed(visibleText));

        visibleCharacters = 0f;
        lastVoiceCount = 0;
        typing = true;
        continueLabel.gameObject.SetActive(false);

        voice.StopAll();
        audioSource.Stop();
        if (line.voice != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(line.voice);
        }

        onLineStarted?.Invoke(currentAsset, lineIndex);

        // A line event may have ended the dialogue or started another.
        if (!active)
            return;

        if (totalCharacters == 0)
            FinishTyping();
    }

    private void AdvanceTypewriter()
    {
        visibleCharacters += currentSpeed * Time.unscaledDeltaTime;
        int count = Mathf.Min(Mathf.FloorToInt(visibleCharacters), totalCharacters);

        if (count != bodyLabel.maxVisibleCharacters)
        {
            bodyLabel.maxVisibleCharacters = count;

            // Characters [lastVoiceCount, count) were just revealed.
            voice.PlayRange(currentVoice, currentNotes, lastVoiceCount, count);
            lastVoiceCount = count;
        }

        if (count >= totalCharacters)
            FinishTyping();
    }

    private void FinishTyping()
    {
        typing = false;
        bodyLabel.maxVisibleCharacters = int.MaxValue;

        bool lastLine = lineIndex >= currentAsset.lines.Length - 1;

        if (lastLine && currentAsset.HasChoices)
            ShowChoices();
        else
            continueLabel.gameObject.SetActive(true);
    }

    private void NextLine()
    {
        lineIndex++;

        if (lineIndex < currentAsset.lines.Length)
        {
            ShowLine(currentAsset.lines[lineIndex]);
            return;
        }

        if (currentAsset.next != null)
            BeginAsset(currentAsset.next);
        else
            EndDialogue();
    }

    private void EndDialogue()
    {
        if (!active)
            return;

        active = false;
        typing = false;
        choiceMode = false;
        endFrame = Time.frameCount;

        audioSource.Stop();
        voice.StopAll();
        ReleasePlayer();

        Action callback = onFinished;
        onFinished = null;
        onLineStarted = null;
        focusProvider = null;
        currentAsset = null;

        OnDialogueEnded?.Invoke();
        callback?.Invoke();
    }

    private void ReleasePlayer()
    {
        if (controlledPlayer && playerController != null)
            playerController.isInspecting = false;

        controlledPlayer = false;
    }

    private void OnDisable()
    {
        if (!active)
            return;

        active = false;
        typing = false;
        choiceMode = false;
        onFinished = null;
        onLineStarted = null;
        focusProvider = null;
        currentAsset = null;
        ReleasePlayer();

        fadeGroup.alpha = 0f;
        panel.SetActive(false);
        choicePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ---------------------------------------------------------------- Choices

    private void ShowChoices()
    {
        activeChoices.Clear();

        foreach (DialogueChoice choice in currentAsset.choices)
        {
            if (choice != null && !string.IsNullOrWhiteSpace(choice.text))
                activeChoices.Add(choice);
        }

        if (activeChoices.Count == 0)
        {
            EndDialogue();
            return;
        }

        choiceMode = true;
        typing = false;
        continueLabel.gameObject.SetActive(false);

        while (choiceLabels.Count < activeChoices.Count)
            choiceLabels.Add(CreateChoiceLabel());

        const float padding = 16f;
        float rowHeight = choiceFontSize + 14f;

        choicePanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, padding * 2f + rowHeight * activeChoices.Count);

        for (int i = 0; i < choiceLabels.Count; i++)
        {
            TextMeshProUGUI label = choiceLabels[i];
            bool used = i < activeChoices.Count;
            label.gameObject.SetActive(used);

            if (!used)
                continue;

            RectTransform rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(0f, -padding - i * rowHeight);
            rect.sizeDelta = new Vector2(-64f, rowHeight);
        }

        choiceIndex = 0;
        RefreshChoiceStyles();
        choicePanel.SetActive(true);
    }

    private void HideChoices()
    {
        choiceMode = false;
        activeChoices.Clear();

        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    private void UpdateChoices(bool confirm)
    {
        int count = activeChoices.Count;
        int move = 0;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            move = -1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            move = 1;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.5f)
            move = -1;
        else if (scroll < -0.5f)
            move = 1;

        if (move != 0)
        {
            choiceIndex = (choiceIndex + move + count) % count;
            RefreshChoiceStyles();
        }

        // Number keys pick directly.
        for (int i = 0; i < count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectChoice(i);
                return;
            }
        }

        if (confirm)
            SelectChoice(choiceIndex);
    }

    private void SelectChoice(int index)
    {
        DialogueAsset next = activeChoices[index].next;
        HideChoices();

        if (next != null)
        {
            emptyHops = 0;
            BeginAsset(next);
        }
        else
        {
            EndDialogue();
        }
    }

    private void RefreshChoiceStyles()
    {
        for (int i = 0; i < activeChoices.Count; i++)
        {
            bool selected = i == choiceIndex;
            string text = (i + 1) + ". " + activeChoices[i].text;

            // A plain ">" marks the selection; the rest are indented to line up with it.
            choiceLabels[i].text = selected ? "> " + text : "<indent=1.1em>" + text;
            choiceLabels[i].color = selected ? choiceSelectedColor : choiceColor;
        }
    }

    // ---------------------------------------------------------------- UI

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("DialogueCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Full-screen root that fades the dialogue and choice boxes together.
        GameObject fadeRoot = new GameObject("FadeRoot", typeof(RectTransform), typeof(CanvasGroup));
        fadeRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform fadeRect = fadeRoot.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeGroup = fadeRoot.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;

        // Main dialogue box.
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(fadeRoot.transform, false);
        panel.GetComponent<Image>().color = panelColor;
        panel.GetComponent<Image>().raycastTarget = false;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.2f, 0f);
        panelRect.anchorMax = new Vector2(0.8f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, panelBottomMargin);
        panelRect.sizeDelta = new Vector2(0f, panelHeight);

        speakerLabel = CreateLabel("Speaker", panelRect, speakerFontSize, speakerColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform speakerRect = speakerLabel.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(0f, -18f);
        speakerRect.sizeDelta = new Vector2(-64f, speakerFontSize + 10f);

        bodyLabel = CreateLabel("Body", panelRect, bodyFontSize, textColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(32f, 40f);
        bodyRect.offsetMax = new Vector2(-32f, -(18f + speakerFontSize + 14f));
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        bodyLabel.overflowMode = TextOverflowModes.Truncate;

        continueLabel = CreateLabel("Continue", panelRect, 20f, textColor, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        RectTransform continueRect = continueLabel.rectTransform;
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-24f, 10f);
        continueRect.sizeDelta = new Vector2(300f, 32f);
        continueLabel.text = continueText;

        // Choice box, sits just above the main box. Height is set when choices are shown.
        choicePanel = new GameObject("ChoicePanel", typeof(RectTransform), typeof(Image));
        choicePanel.transform.SetParent(fadeRoot.transform, false);
        choicePanel.GetComponent<Image>().color = panelColor;
        choicePanel.GetComponent<Image>().raycastTarget = false;

        RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
        choiceRect.anchorMin = new Vector2(0.2f, 0f);
        choiceRect.anchorMax = new Vector2(0.8f, 0f);
        choiceRect.pivot = new Vector2(0.5f, 0f);
        choiceRect.anchoredPosition = new Vector2(0f, panelBottomMargin + panelHeight + 12f);

        // Interact prompt, shown while looking at something talkable.
        interactPromptLabel = CreateLabel("InteractPrompt", canvasObject.transform, interactPromptFontSize, new Color(0.75f, 0.75f, 0.7f, 0.55f), FontStyles.Normal, TextAlignmentOptions.Center);
        interactPromptLabel.outlineWidth = 0.12f;
        interactPromptLabel.outlineColor = Color.black;
        RectTransform promptRect = interactPromptLabel.rectTransform;
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, interactPromptOffsetY);
        promptRect.sizeDelta = new Vector2(600f, interactPromptFontSize + 12f);
    }

    private TextMeshProUGUI CreateChoiceLabel()
    {
        TextMeshProUGUI label = CreateLabel(
            "Choice" + choiceLabels.Count,
            choicePanel.transform,
            choiceFontSize,
            choiceColor,
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        return label;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, float size, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }
}
