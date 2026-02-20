using UnityEngine;
using UnityEngine.UI;
using Eidolon.SDK.Core;

/// <summary>
/// VAYDE Guard Standard UI Controller v0.3.0
/// Controls the display of NPC speech, emotions, actions, and reputation.
/// </summary>
public class SimpleChatUI : MonoBehaviour
{
    // Supported display languages
    public enum UILanguage { English, Russian }

    [Header("Localization")]
    [Tooltip("Language used for UI labels and status messages.")]
    public UILanguage language = UILanguage.English;

    [Header("Core References")]
    public EidolonBridge bridge;
    public InputField inputField;
    public Button sendButton;

    [Header("Display Areas")]
    [Tooltip("Main NPC speech output field.")]
    public Text gormTextArea;

    [Tooltip("NPC emotional state field (e.g. Suspicious).")]
    public Text emotionArea;

    [Tooltip("NPC physical action field (e.g. crosses arms).")]
    public Text actionArea;

    [Tooltip("Player reputation score display field.")]
    public Text reputationArea;

    // Localized strings
    private string L(string en, string ru) => language == UILanguage.Russian ? ru : en;

    void Start()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClick);

        Debug.Log("[EIDOLON] UI System initialized. Waiting for input...");
    }

    async void OnSendClick()
    {
        string text = inputField.text;
        if (string.IsNullOrEmpty(text)) return;

        // Lock input while waiting for the server response
        SetUIState(false, L("Gorm is thinking...", "Горм обдумывает ответ..."));

        try
        {
            var res = await bridge.SendInteraction(text);

            // NPC speech
            if (gormTextArea != null)
                gormTextArea.text = res.ResponseText;

            // Emotional state
            if (emotionArea != null)
                emotionArea.text = res.EmotionalState;

            // Physical action
            if (actionArea != null)
                actionArea.text = res.VisualCue;

            // Reputation with color feedback
            UpdateReputationDisplay(res.Reputation);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UI ERROR] Failed to process interaction: {ex.Message}");
            if (gormTextArea != null)
                gormTextArea.text = L(
                    "<color=red>*Connection to Gorm lost*</color>",
                    "<color=red>*Связь с Гормом прервалась*</color>"
                );
        }
        finally
        {
            SetUIState(true, "");
        }
    }

    private void SetUIState(bool isInteractable, string statusText)
    {
        if (!string.IsNullOrEmpty(statusText) && gormTextArea != null)
            gormTextArea.text = statusText;

        if (inputField != null) inputField.interactable = isInteractable;
        if (sendButton != null) sendButton.interactable = isInteractable;
        if (isInteractable && inputField != null) inputField.ActivateInputField();
    }

    private void UpdateReputationDisplay(int repValue)
    {
        if (reputationArea == null) return;

        reputationArea.text = L($"Reputation: {repValue}", $"Репутация: {repValue}");

        if (repValue < 0)
            reputationArea.color = Color.red;       // Hostile
        else if (repValue > 10)
            reputationArea.color = Color.green;     // Trusted
        else
            reputationArea.color = Color.white;     // Neutral
    }
}