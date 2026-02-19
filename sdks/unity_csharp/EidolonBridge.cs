// ---------------------------------------------------------------------------
// EidolonBridge.cs — Core Unity SDK Bridge for EIDOLON
// Copyright (c) VAYDE. All rights reserved.
// ---------------------------------------------------------------------------

using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Eidolon.SDK.Core
{
    // ======================================================================
    // DATA STRUCTURES
    // ======================================================================

    /// <summary>
    /// Defines who an NPC is — personality, background, and speech style.
    /// Mirrors <c>NPCPersonality</c> from the Python eidolon_core.
    /// </summary>
    [Serializable]
    public class PersonalityConfig
    {
        [SerializeField] private string npcName = "Unknown";
        [SerializeField] private string[] traits = Array.Empty<string>();
        [SerializeField] private string speechStyle = "";
        [SerializeField] private string background = "";

        /// <summary>Display name of the NPC character.</summary>
        public string NPCName => npcName;

        /// <summary>Personality traits (e.g. "brave", "sarcastic").</summary>
        public string[] Traits => traits;

        /// <summary>How the character speaks (e.g. "formal medieval English").</summary>
        public string SpeechStyle => speechStyle;

        /// <summary>Backstory and world context for the character.</summary>
        public string Background => background;
    }

    /// <summary>
    /// Structured response returned by the Gemini API, parsed from JSON.
    /// Mirrors <c>NPCResponse</c> from the Python eidolon_core.
    /// </summary>
    [Serializable]
    public class EidolonResponse
    {
        [SerializeField] private string response_text = "";
        [SerializeField] private string emotional_state = "neutral";
        [SerializeField] private string visual_cue = "stands still";

        /// <summary>What the NPC says, fully in character.</summary>
        public string ResponseText => response_text;

        /// <summary>Current emotional state label (e.g. "angry", "joyful").</summary>
        public string EmotionalState => emotional_state;

        /// <summary>Brief physical action or body language description.</summary>
        public string VisualCue => visual_cue;

        /// <summary>
        /// Parses a JSON string into an <see cref="EidolonResponse"/>.
        /// Handles markdown code-fence wrapping that LLMs sometimes produce.
        /// </summary>
        /// <param name="json">Raw JSON string from the API.</param>
        /// <returns>A populated <see cref="EidolonResponse"/>, or a fallback on parse failure.</returns>
        public static EidolonResponse FromJson(string json)
        {
            string cleaned = json.Trim();

            // Strip optional markdown code fences (```json ... ```)
            if (cleaned.StartsWith("```"))
            {
                int firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                    cleaned = cleaned.Substring(firstNewline + 1);

                if (cleaned.EndsWith("```"))
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);

                cleaned = cleaned.Trim();
            }

            try
            {
                return JsonUtility.FromJson<EidolonResponse>(cleaned);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EidolonBridge] JSON parse error: {ex.Message}\nRaw: {json}");
                return new EidolonResponse
                {
                    response_text = cleaned,
                    emotional_state = "neutral",
                    visual_cue = "stands still"
                };
            }
        }

        public override string ToString() =>
            $"[{EmotionalState}] \"{ResponseText}\" ({VisualCue})";
    }

    // ======================================================================
    // GEMINI API PAYLOAD DTOs (internal)
    // ======================================================================

    /// <summary>Request body for Gemini REST API <c>generateContent</c> endpoint.</summary>
    [Serializable]
    internal class GeminiRequest
    {
        public GeminiContent[] contents;
        public GeminiSystemInstruction system_instruction;
    }

    [Serializable]
    internal class GeminiSystemInstruction
    {
        public GeminiPart[] parts;
    }

    [Serializable]
    internal class GeminiContent
    {
        public string role;
        public GeminiPart[] parts;
    }

    [Serializable]
    internal class GeminiPart
    {
        public string text;
    }

    /// <summary>Minimal response shape from Gemini <c>generateContent</c>.</summary>
    [Serializable]
    internal class GeminiResponse
    {
        public GeminiCandidate[] candidates;
    }

    [Serializable]
    internal class GeminiCandidate
    {
        public GeminiContent content;
    }

    // ======================================================================
    // EIDOLON BRIDGE — MAIN CLASS
    // ======================================================================

    /// <summary>
    /// Core bridge between Unity and the Google Gemini API.
    /// Attach this MonoBehaviour to any GameObject to enable AI-NPC interactions.
    /// </summary>
    [AddComponentMenu("EIDOLON/Eidolon Bridge")]
    public class EidolonBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Header("Gemini API Configuration")]
        [Tooltip("Your Google AI Studio API key. WARNING: Move to a secure config (e.g. ScriptableObject or environment variable) before shipping.")]
        [SerializeField] private string apiKey = "";

        [Tooltip("Gemini model identifier. Defaults to gemini-2.5-flash.")]
        [SerializeField] private string modelName = "gemini-2.5-flash";

        [Header("NPC Identity")]
        [Tooltip("Personality configuration for the NPC powered by this bridge.")]
        [SerializeField] private PersonalityConfig personality = new PersonalityConfig();

        [Header("Events")]
        [SerializeField] private UnityEvent<EidolonResponse> onResponseReceived = new UnityEvent<EidolonResponse>();

        // ------------------------------------------------------------------
        // Public properties
        // ------------------------------------------------------------------

        /// <summary>Event fired every time a valid response is received from the API.</summary>
        public UnityEvent<EidolonResponse> OnResponseReceived => onResponseReceived;

        /// <summary>The current personality configuration. Can be swapped at runtime.</summary>
        public PersonalityConfig Personality
        {
            get => personality;
            set => personality = value ?? throw new ArgumentNullException(nameof(value));
        }

        // ------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------

        private const string GeminiEndpointTemplate =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        // ------------------------------------------------------------------
        // Core public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Sends a player interaction to the Gemini API and returns a structured
        /// in-character NPC response.
        /// </summary>
        /// <param name="playerInput">The text the player typed or spoke.</param>
        /// <returns>
        /// An <see cref="EidolonResponse"/> with the NPC's reply, emotional state,
        /// and visual cue. Returns a fallback response on failure.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="playerInput"/> is null or empty.</exception>
        public async Task<EidolonResponse> SendInteraction(string playerInput)
        {
            // --- Validation ---
            if (string.IsNullOrWhiteSpace(playerInput))
                throw new ArgumentException("Player input cannot be null or empty.", nameof(playerInput));

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogError("[EidolonBridge] API key is not set. Assign it in the Inspector or via code.");
                return CreateFallbackResponse("error");
            }

            // --- Check network reachability ---
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("[EidolonBridge] No internet connection detected.");
                return CreateFallbackResponse("no_internet");
            }

            // --- Build request ---
            string endpoint = string.Format(GeminiEndpointTemplate, modelName, apiKey);
            string systemPrompt = BuildSystemInstruction(personality);
            string requestBody = BuildRequestJson(systemPrompt, playerInput);

            // --- Send request ---
            EidolonResponse response;

            try
            {
                string rawResponse = await PostJsonAsync(endpoint, requestBody);
                string npcText = ExtractTextFromGeminiResponse(rawResponse);
                response = EidolonResponse.FromJson(npcText);
            }
            catch (EidolonApiException ex)
            {
                Debug.LogError($"[EidolonBridge] API error ({ex.StatusCode}): {ex.Message}");
                response = CreateFallbackResponse("api_error");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EidolonBridge] Unexpected error: {ex.Message}");
                response = CreateFallbackResponse("error");
            }

            // --- Notify listeners ---
            onResponseReceived?.Invoke(response);
            OnInteractionCompleted(playerInput, response);

            return response;
        }

        // ------------------------------------------------------------------
        // Database / logging hook
        // ------------------------------------------------------------------

        /// <summary>
        /// Called after every completed interaction. Override in a subclass to
        /// persist interactions to SQL, local storage, analytics, etc.
        /// </summary>
        /// <param name="input">The original player input.</param>
        /// <param name="response">The parsed NPC response.</param>
        protected virtual void OnInteractionCompleted(string input, EidolonResponse response)
        {
            // Default: no-op. Subclass and override to add persistence.
        }

        // ------------------------------------------------------------------
        // System prompt builder
        // ------------------------------------------------------------------

        /// <summary>
        /// Constructs the system-level prompt that locks the LLM into character
        /// and forces structured JSON output.
        /// </summary>
        private static string BuildSystemInstruction(PersonalityConfig config)
        {
            string traits = config.Traits != null && config.Traits.Length > 0
                ? string.Join(", ", config.Traits)
                : "none specified";

            return new StringBuilder()
                .AppendLine($"You are {config.NPCName}, a living character in a game world.")
                .AppendLine($"Personality traits: {traits}.")
                .AppendLine($"Background: {config.Background}")
                .AppendLine($"Speech style: {config.SpeechStyle}")
                .AppendLine()
                .AppendLine("ABSOLUTE RULES — follow these without exception:")
                .AppendLine("1. You ARE this character. You are NOT an AI, language model, or assistant.")
                .AppendLine("2. NEVER break character. NEVER reveal you are artificial in any way.")
                .AppendLine("3. NEVER use phrases like 'As an AI…', 'I'm a language model…', or similar.")
                .AppendLine("4. Stay consistent with your background, traits, and speech style at all times.")
                .AppendLine("5. React emotionally as your character would — show suspicion, joy, anger, etc.")
                .AppendLine()
                .AppendLine("RESPONSE FORMAT — you MUST reply with valid JSON and nothing else:")
                .AppendLine("{")
                .AppendLine("  \"response_text\": \"<what you say in character>\",")
                .AppendLine("  \"emotional_state\": \"<one-word or short emotion label>\",")
                .AppendLine("  \"visual_cue\": \"<brief physical action description>\"")
                .AppendLine("}")
                .ToString();
        }

        // ------------------------------------------------------------------
        // Request / response helpers
        // ------------------------------------------------------------------

        /// <summary>Builds the full Gemini API request JSON body.</summary>
        private static string BuildRequestJson(string systemPrompt, string userMessage)
        {
            var request = new GeminiRequest
            {
                system_instruction = new GeminiSystemInstruction
                {
                    parts = new[] { new GeminiPart { text = systemPrompt } }
                },
                contents = new[]
                {
                    new GeminiContent
                    {
                        role = "user",
                        parts = new[] { new GeminiPart { text = userMessage } }
                    }
                }
            };

            return JsonUtility.ToJson(request);
        }

        /// <summary>
        /// Sends a POST request with a JSON body and returns the raw response string.
        /// Uses <see cref="UnityWebRequest"/> wrapped in async/await via
        /// <see cref="TaskCompletionSource{T}"/>.
        /// </summary>
        private static async Task<string> PostJsonAsync(string url, string jsonBody)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var tcs = new TaskCompletionSource<string>();
                var operation = request.SendWebRequest();

                operation.completed += _ =>
                {
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        tcs.SetException(new EidolonApiException(
                            0, $"Connection error: {request.error}"));
                    }
                    else if (request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        tcs.SetException(new EidolonApiException(
                            request.responseCode,
                            $"HTTP {request.responseCode}: {request.downloadHandler.text}"));
                    }
                    else
                    {
                        tcs.SetResult(request.downloadHandler.text);
                    }
                };

                return await tcs.Task;
            }
        }

        /// <summary>
        /// Extracts the text content from the first candidate of a Gemini
        /// <c>generateContent</c> response.
        /// </summary>
        private static string ExtractTextFromGeminiResponse(string rawJson)
        {
            try
            {
                var geminiResponse = JsonUtility.FromJson<GeminiResponse>(rawJson);

                if (geminiResponse?.candidates != null
                    && geminiResponse.candidates.Length > 0
                    && geminiResponse.candidates[0].content?.parts != null
                    && geminiResponse.candidates[0].content.parts.Length > 0)
                {
                    return geminiResponse.candidates[0].content.parts[0].text;
                }

                Debug.LogWarning("[EidolonBridge] Gemini response contained no candidates.");
                return rawJson;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EidolonBridge] Failed to extract Gemini response: {ex.Message}");
                return rawJson;
            }
        }

        // ------------------------------------------------------------------
        // Fallback responses
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns a safe, in-character fallback response when the API call fails.
        /// Keeps immersion intact even during errors.
        /// </summary>
        private static EidolonResponse CreateFallbackResponse(string reason)
        {
            switch (reason)
            {
                case "no_internet":
                    return EidolonResponse.FromJson(
                        "{\"response_text\":\"*looks around, distracted by something in the distance*\"," +
                        "\"emotional_state\":\"distracted\"," +
                        "\"visual_cue\":\"gazes toward the horizon, not hearing you\"}");

                case "api_error":
                    return EidolonResponse.FromJson(
                        "{\"response_text\":\"*stares silently, unwilling to continue the conversation*\"," +
                        "\"emotional_state\":\"guarded\"," +
                        "\"visual_cue\":\"crosses arms and looks away\"}");

                default: // "error"
                    return EidolonResponse.FromJson(
                        "{\"response_text\":\"*seems momentarily dazed, then shakes it off*\"," +
                        "\"emotional_state\":\"confused\"," +
                        "\"visual_cue\":\"rubs temple and blinks\"}");
            }
        }
    }

    // ======================================================================
    // CUSTOM EXCEPTION
    // ======================================================================

    /// <summary>
    /// Represents an error returned by the Gemini API or a network failure
    /// during an EIDOLON interaction.
    /// </summary>
    public class EidolonApiException : Exception
    {
        /// <summary>HTTP status code (0 for connection errors).</summary>
        public long StatusCode { get; }

        public EidolonApiException(long statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
