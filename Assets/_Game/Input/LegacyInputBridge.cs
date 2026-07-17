// =============================================================================
// LegacyInputBridge.cs
// -----------------------------------------------------------------------------
// "Both" veya "Input System Package" modunda eski UnityEngine.Input API'si
// kapalıdır. Bu yardımcı sınıf, yeni Input System'i kullanarak eski
// GetKey/GetKeyDown/GetKeyUp çağrılarını taklit eder.
//
// Yeni Input System'in Keyboard ve Mouse API'lerini sarmalayarak
// mevcut kodun (GameManager, BuildManager, DebugOverlay, DebugConsole,
// CallMomSkill) minimum değişiklikle çalışmasını sağlar.
//
// Inspector'dan değiştirilecek bir şey yok; tüm API static.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace GoodNightMyAngel.InputBridge
{
    /// <summary>
    /// Eski UnityEngine.Input API'sini yeni Input System üzerinden sarmalayan
    /// statik köprü. Hem eski hem yeni sistemi bir arada kullanan projeler için.
    /// </summary>
    public static class LegacyInputBridge
    {
        // Keyboard ve Mouse için kısayollar.
        private static Keyboard _kb => Keyboard.current;
        private static Mouse _ms => Mouse.current;

        // KeyCode -> Key eşlemesi. Sık kullanılanları destekler; eksik olan
        // projeden eklenebilir. Tam liste ihtiyaç halinde genişletilir.
        private static Key? ToKey(KeyCode k)
        {
            switch (k)
            {
                // Harfler
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;

                // Rakamlar (üst sıra)
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;

                // Fonksiyon tuşları
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;

                // Ok tuşları
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;

                // Özel tuşlar
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.BackQuote: return Key.Backquote;

                // Mouse butonları
                case KeyCode.Mouse0: return null; // sol tık ayrı ele alınır
                case KeyCode.Mouse1: return null; // sağ tık ayrı ele alınır

                default: return null;
            }
        }

        // --------------------------------------------------------------------
        // PUBLIC API — eski Input.GetKey* yerine kullanılır
        // --------------------------------------------------------------------

        /// <summary>Tuş şu an basılı mı?</summary>
        public static bool GetKey(KeyCode key)
        {
            if (key == KeyCode.Mouse0) return _ms != null && _ms.leftButton.isPressed;
            if (key == KeyCode.Mouse1) return _ms != null && _ms.rightButton.isPressed;
            var k = ToKey(key);
            return k.HasValue && _kb != null && _kb[k.Value].isPressed;
        }

        /// <summary>Tuş bu frame basıldı mı?</summary>
        public static bool GetKeyDown(KeyCode key)
        {
            if (key == KeyCode.Mouse0) return _ms != null && _ms.leftButton.wasPressedThisFrame;
            if (key == KeyCode.Mouse1) return _ms != null && _ms.rightButton.wasPressedThisFrame;
            var k = ToKey(key);
            return k.HasValue && _kb != null && _kb[k.Value].wasPressedThisFrame;
        }

        /// <summary>Tuş bu frame bırakıldı mı?</summary>
        public static bool GetKeyUp(KeyCode key)
        {
            if (key == KeyCode.Mouse0) return _ms != null && _ms.leftButton.wasReleasedThisFrame;
            if (key == KeyCode.Mouse1) return _ms != null && _ms.rightButton.wasReleasedThisFrame;
            var k = ToKey(key);
            return k.HasValue && _kb != null && _kb[k.Value].wasReleasedThisFrame;
        }

        /// <summary>Mouse scroll değeri (eski Input.mouseScrollDelta benzeri).</summary>
        public static Vector2 mouseScrollDelta =>
            _ms != null ? _ms.scroll.ReadValue() : Vector2.zero;

        /// <summary>Mouse world pozisyonu (eski Input.mousePosition).</summary>
        public static Vector3 mousePosition =>
            _ms != null ? (Vector3)_ms.position.ReadValue() : Vector3.zero;
    }
}
