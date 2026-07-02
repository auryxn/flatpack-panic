using UnityEngine;

namespace FlatpackPanic
{
    public class GameHud : MonoBehaviour
    {
        private GUIStyle _box, _title, _small;

        private void OnGUI()
        {
            Init();
            var g = FlatpackGame.Instance;
            if (g == null) return;

            if (Input.GetKeyDown(KeyCode.H)) g.HelpVisible = !g.HelpVisible;
            if (Input.GetKeyDown(KeyCode.R)) g.ResetCargo();

            DrawStatusHud(g);
            DrawInteractionPrompt(g);
            DrawWinScreen(g);
        }

        private void DrawStatusHud(FlatpackGame g)
        {
            if (!g.HelpVisible)
            {
                var hiddenStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    normal = { textColor = new Color(1f, 1f, 1f, .65f) }
                };
                GUI.Label(new Rect(18, 18, 180, 24), "H — show HUD", hiddenStyle);
                return;
            }

            var height = g.HelpVisible ? 318 : 162;
            GUILayout.BeginArea(new Rect(18, 18, 500, height), GUIContent.none, _box);
            GUILayout.Label("FLATPACK PANIC — DELIVERY LOOP", _title);
            GUILayout.Label($"Objective: {g.MissionText}", _small);
            GUILayout.Label($"Loaded: {g.LoadedCount}/{g.Cargo.Count}   Delivered: {g.DeliveredCount}/{g.Cargo.Count}   Damage: {g.TotalDamage:0.0}", _small);
            GUILayout.Label($"Timer: {g.ElapsedSeconds:0}s   Rank: {g.Rank}", _small);
            GUILayout.Label("H — hide HUD", _small);
            GUILayout.Space(8);
            GUILayout.Label("On foot: first-person. In van: third-person chase camera.", _small);
            GUILayout.Label("W/S or ↑/↓ gas/reverse · A/D or ←/→ steer · Space brake", _small);
            GUILayout.Label("Mouse look sensitivity increased · Shift sprint · F only near driver door", _small);
            GUILayout.Label("E grab/drop/load/take box · R reset", _small);
            GUILayout.Label("Loop: pick cargo → rear van bay + E to load → drive to green beacon → behind van + E to take → drop in green zone.", _small);
            GUILayout.EndArea();
        }

        private void DrawInteractionPrompt(FlatpackGame g)
        {
            if (!g.HelpVisible || g.Player == null || string.IsNullOrWhiteSpace(g.Player.InteractionPrompt)) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(18, 18, 10, 10),
                normal = { textColor = Color.white }
            };

            var width = 520f;
            var height = 58f;
            GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height * 0.72f, width, height), g.Player.InteractionPrompt, style);

            var cross = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, .85f, .15f) }
            };
            GUI.Label(new Rect(Screen.width / 2f - 20f, Screen.height / 2f - 18f, 40f, 40f), "+", cross);
        }

        private void DrawWinScreen(FlatpackGame g)
        {
            if (!g.MissionComplete) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(22, 22, 18, 18),
                normal = { textColor = Color.white }
            };

            var text = $"DELIVERY COMPLETE!\n\nTime: {g.ElapsedSeconds:0}s\nDamage: {g.TotalDamage:0.0}\nRank: {g.Rank}\n\nPress R to restart";
            GUI.Box(new Rect(Screen.width / 2f - 240f, Screen.height / 2f - 140f, 480f, 280f), text, style);
        }

        private void Init()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14) };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .82f, .1f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Color.white } };
        }
    }
}
