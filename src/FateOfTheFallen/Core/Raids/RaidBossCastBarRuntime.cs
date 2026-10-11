using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Lightweight PoC boss cast bar for scripted raid mechanics.
    /// Native spell casting is not used for these scripted mechanics, so this
    /// provides a clear player-facing cast name and countdown without coupling
    /// the encounter controller to Erenshor's spell system.
    /// </summary>
    internal static class RaidBossCastBarRuntime
    {
        private sealed class CastBarView : MonoBehaviour
        {
            private GUIStyle _nameStyle;
            private GUIStyle _timeStyle;

            private void OnGUI()
            {
                if (!_visible ||
                    string.IsNullOrEmpty(_castName))
                {
                    return;
                }

                float remaining =
                    Mathf.Max(
                        0f,
                        _castEndRealtime - Time.realtimeSinceStartup);

                float progress =
                    _castDuration > 0f
                        ? Mathf.Clamp01(1f - (remaining / _castDuration))
                        : 1f;

                float width =
                    Mathf.Min(
                        460f,
                        Screen.width * 0.42f);

                float height =
                    54f;

                Rect outer =
                    new Rect(
                        (Screen.width - width) * 0.5f,
                        Screen.height * 0.72f,
                        width,
                        height);

                Color previousColor =
                    GUI.color;

                GUI.color =
                    new Color(
                        0.04f,
                        0.04f,
                        0.05f,
                        0.92f);

                GUI.DrawTexture(
                    outer,
                    Texture2D.whiteTexture);

                Rect fill =
                    new Rect(
                        outer.x + 4f,
                        outer.y + 27f,
                        (outer.width - 8f) * progress,
                        21f);

                GUI.color =
                    new Color(
                        0.53f,
                        0.10f,
                        0.64f,
                        0.95f);

                GUI.DrawTexture(
                    fill,
                    Texture2D.whiteTexture);

                GUI.color =
                    Color.white;

                EnsureStyles();

                GUI.Label(
                    new Rect(
                        outer.x + 10f,
                        outer.y + 3f,
                        outer.width - 90f,
                        24f),
                    _castName,
                    _nameStyle);

                GUI.Label(
                    new Rect(
                        outer.x + outer.width - 80f,
                        outer.y + 3f,
                        70f,
                        24f),
                    remaining.ToString("0.0"),
                    _timeStyle);

                GUI.color =
                    previousColor;
            }

            private void EnsureStyles()
            {
                if (_nameStyle == null)
                {
                    _nameStyle =
                        new GUIStyle(
                            GUI.skin.label);

                    _nameStyle.fontSize =
                        16;

                    _nameStyle.fontStyle =
                        FontStyle.Bold;

                    _nameStyle.alignment =
                        TextAnchor.MiddleLeft;
                }

                if (_timeStyle == null)
                {
                    _timeStyle =
                        new GUIStyle(
                            GUI.skin.label);

                    _timeStyle.fontSize =
                        16;

                    _timeStyle.fontStyle =
                        FontStyle.Bold;

                    _timeStyle.alignment =
                        TextAnchor.MiddleRight;
                }
            }
        }

        private static CastBarView _view;
        private static bool _visible;
        private static string _castName;
        private static float _castDuration;
        private static float _castEndRealtime;

        internal static void Show(
            string castName,
            float durationSeconds)
        {
            EnsureView();

            _castName =
                castName ?? string.Empty;

            _castDuration =
                Mathf.Max(
                    0.01f,
                    durationSeconds);

            _castEndRealtime =
                Time.realtimeSinceStartup + _castDuration;

            _visible =
                true;
        }

        internal static void Hide()
        {
            _visible =
                false;

            _castName =
                null;

            _castDuration =
                0f;

            _castEndRealtime =
                0f;
        }

        private static void EnsureView()
        {
            if (_view != null)
            {
                return;
            }

            GameObject host =
                new GameObject(
                    "FOTF_RaidBossCastBar");

            _view =
                host.AddComponent<CastBarView>();
        }
    }
}
