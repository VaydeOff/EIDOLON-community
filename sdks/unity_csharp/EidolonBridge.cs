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
        public int reputation = 0;

        /// <summary>What the NPC says, fully in character.</summary>
        public string ResponseText => response_text;

        /// <summary>Current emotional state label (e.g. "angry", "joyful").</summary>
        public string EmotionalState => emotional_state;

        /// <summary>Brief physical action or body language description.</summary>
        public string VisualCue => visual_cue;

        /// <summary>Current player reputation score with this NPC.</summary>
        public int Reputation => reputation;

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
    // SERVER REQUEST DTO
    // ======================================================================

    /// <summary>Request body sent to the EIDOLON FastAPI bridge server.</summary>
    [System.Serializable]
    public class ChatRequestData
    {
        public string user_id;
        public string message;
    }

    // ======================================================================
    // EIDOLON BRIDGE — MAIN CLASS
    // ======================================================================

    /// <summary>
    /// Core bridge between Unity and the EIDOLON FastAPI server.
    /// Attach this MonoBehaviour to any GameObject to enable AI-NPC interactions.
    /// The server handles LLM calls, memory, and reputation internally.
    /// </summary>
    [AddComponentMenu("EIDOLON/Eidolon Bridge")]
    public class EidolonBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Header("EIDOLON Server Configuration")]
        [Tooltip("URL of the EIDOLON FastAPI bridge server.")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:8000/chat";

        [Tooltip("Player identifier sent with each request.")]
        [SerializeField] private string userId = "Piligrim";

        [Header("NPC Identity")]
        [Tooltip("Personality configuration for the NPC powered by this bridge.")]
        [SerializeField] private PersonalityConfig personality = new PersonalityConfig();

        [Header("Events")]
        [SerializeField] private UnityEvent<EidolonResponse> onResponseReceived = new UnityEvent<EidolonResponse>();

        // ------------------------------------------------------------------
        // Public properties
        // ------------------------------------------------------------------

        /// <summary>Event fired every time a valid response is received from the server.</summary>
        public UnityEvent<EidolonResponse> OnResponseReceived => onResponseReceived;

        /// <summary>The current personality configuration. Can be swapped at runtime.</summary>
        public PersonalityConfig Personality
        {
            get => personality;
            set => personality = value ?? throw new ArgumentNullException(nameof(value));
        }

        // ------------------------------------------------------------------
        // Core public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Sends a player interaction to the EIDOLON bridge server and returns
        /// a structured in-character NPC response.
        /// The server handles LLM calls, memory retrieval, and reputation updates.
        /// </summary>
        /// <param name="playerInput">The text the player typed or spoke.</param>
        /// <returns>
        /// An <see cref="EidolonResponse"/> with the NPC's reply, emotional state,
        /// and visual cue. Returns a fallback response on failure.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="playerInput"/> is null or empty.</exception>
        public async Task<EidolonResponse> SendInteraction(string playerInput)
        {
            // 1. Формируем данные через Serializable класс
            var requestBody = new ChatRequestData
            {
                user_id = userId,
                message = playerInput
            };
            string jsonPayload = JsonUtility.ToJson(requestBody);

            Debug.Log("[EIDOLON] Sending: " + jsonPayload); // Увидим, что улетает

            try
            {
                string rawResponse = await PostJsonAsync(serverUrl, jsonPayload);
                Debug.Log("[EIDOLON] Raw Response: " + rawResponse); // Увидим, что прилетает

                var response = EidolonResponse.FromJson(rawResponse);
                onResponseReceived?.Invoke(response);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EIDOLON] Bridge Error: {ex.Message}");
                return CreateFallbackResponse("error");
            }
        }

        // ------------------------------------------------------------------
        // HTTP helper
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            Debug.Log("[EIDOLON] Здравствуй! Bridge is ready.");
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
