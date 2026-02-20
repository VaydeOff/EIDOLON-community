using UnityEngine;
using UnityEngine.UI;
using Eidolon.SDK.Core;

/// <summary>
/// VAYDE Guard Standard UI Controller v0.3.0
/// Управляет отображением речи, эмоций, действий и репутации NPC.
/// </summary>
public class SimpleChatUI : MonoBehaviour
{
    [Header("Core References")]
    public EidolonBridge bridge;
    public InputField inputField;
    public Button sendButton;

    [Header("Display Areas")]
    [Tooltip("Поле для основной речи Горма")]
    public Text gormTextArea;

    [Tooltip("Поле для эмоции NPC (например: Подозрительно)")]
    public Text emotionArea;

    [Tooltip("Поле для физического действия NPC (например: скрещивает руки)")]
    public Text actionArea;

    [Tooltip("Поле для отображения уровня репутации")]
    public Text reputationArea;

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

        // Визуальный фидбек: блокируем ввод и показываем статус
        SetUIState(false, "Горм обдумывает ответ...");

        try
        {
            var res = await bridge.SendInteraction(text);

            // 1. Основная речь NPC
            if (gormTextArea != null)
                gormTextArea.text = res.ResponseText;

            // 2. Эмоция — отдельное поле
            if (emotionArea != null)
                emotionArea.text = res.EmotionalState;

            // 3. Действие — отдельное поле
            if (actionArea != null)
                actionArea.text = res.VisualCue;

            // 4. Репутация с цветовой индикацией
            UpdateReputationDisplay(res.Reputation);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UI ERROR] Failed to process interaction: {ex.Message}");
            if (gormTextArea != null)
                gormTextArea.text = "<color=red>*Связь с Гормом прервалась*</color>";
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

        reputationArea.text = $"Репутация: {repValue}";

        if (repValue < 0)
            reputationArea.color = Color.red;      // Враждебность
        else if (repValue > 10)
            reputationArea.color = Color.green;     // Доверие
        else
            reputationArea.color = Color.white;     // Нейтралитет
    }
}