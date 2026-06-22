using UnityEditor;
using UnityEngine;

namespace Game.Inventory.EditorTools
{
    /// <summary>
    /// GridShape를 인스펙터에서 "칸을 클릭해 칠하는" 격자 UI로 그린다.
    /// 디자이너가 좌표/배열을 손으로 입력할 필요 없이 아이템·실루엣 형태를 직접 그릴 수 있다.
    /// Width/Height를 바꾸면 기존 칠을 보존하며 격자가 리사이즈된다.
    /// </summary>
    [CustomPropertyDrawer(typeof(GridShape))]
    public class GridShapeDrawer : PropertyDrawer
    {
        const float Cell = 22f;
        const float Pad  = 2f;
        static readonly Color OnColor  = new Color(0.45f, 0.80f, 0.55f);
        static readonly Color OffColor = new Color(0.28f, 0.28f, 0.28f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var wProp = property.FindPropertyRelative("width");
            var hProp = property.FindPropertyRelative("height");
            var cProp = property.FindPropertyRelative("cells");

            EditorGUI.BeginProperty(position, label, property);

            float lineH = EditorGUIUtility.singleLineHeight;
            var line = new Rect(position.x, position.y, position.width, lineH);
            EditorGUI.LabelField(line, label, EditorStyles.boldLabel);

            // Width / Height 입력
            line.y += lineH + Pad;
            float half = position.width * 0.5f;
            int newW = Mathf.Max(1, EditorGUI.IntField(new Rect(line.x,        line.y, half - 4, lineH), "Width",  wProp.intValue));
            int newH = Mathf.Max(1, EditorGUI.IntField(new Rect(line.x + half, line.y, half - 4, lineH), "Height", hProp.intValue));
            EnsureSize(wProp, hProp, cProp, newW, newH);

            // 칸 격자 (클릭 토글)
            float gridTop = line.y + lineH + Pad * 2;
            int w = wProp.intValue, h = hProp.intValue;
            var prevBg = GUI.backgroundColor;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (i >= cProp.arraySize) continue;
                var el = cProp.GetArrayElementAtIndex(i);
                var r  = new Rect(position.x + x * (Cell + Pad), gridTop + y * (Cell + Pad), Cell, Cell);
                GUI.backgroundColor = el.boolValue ? OnColor : OffColor;
                if (GUI.Button(r, GUIContent.none)) el.boolValue = !el.boolValue;
            }
            GUI.backgroundColor = prevBg;

            EditorGUI.EndProperty();
        }

        /// <summary>Width/Height 변경 시 기존 칠을 보존하며 cells 배열을 다시 깐다.</summary>
        static void EnsureSize(SerializedProperty w, SerializedProperty h, SerializedProperty cells, int newW, int newH)
        {
            if (newW == w.intValue && newH == h.intValue && cells.arraySize == newW * newH) return;

            int oldW = w.intValue, oldH = h.intValue;
            var old = new bool[cells.arraySize];
            for (int i = 0; i < old.Length; i++) old[i] = cells.GetArrayElementAtIndex(i).boolValue;

            cells.arraySize = newW * newH;
            for (int y = 0; y < newH; y++)
            for (int x = 0; x < newW; x++)
            {
                int srcI = y * oldW + x;
                bool val = x < oldW && y < oldH && srcI < old.Length && old[srcI];
                cells.GetArrayElementAtIndex(y * newW + x).boolValue = val;
            }
            w.intValue = newW;
            h.intValue = newH;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int h = Mathf.Max(1, property.FindPropertyRelative("height").intValue);
            return EditorGUIUtility.singleLineHeight * 2 + Pad * 3 + h * (Cell + Pad);
        }
    }
}