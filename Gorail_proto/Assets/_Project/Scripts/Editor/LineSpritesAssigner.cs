using System.Collections.Generic;
using System.Linq;
using Game.Subway;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 호선별 멀티플 스프라이트를 UIBezierLine Image에 자동 할당하고 Set Native Size 적용.
    ///
    /// 스프라이트 명명 규칙: {lineId}_{구간인덱스}
    ///   예) line1_0 = 1호선 첫 번째 구간, line1_1 = 두 번째 구간 …
    /// 구간 인덱스는 networkData.lines 내 stations 배열 순서 기준 (stations[i] → stations[i+1]).
    ///
    /// 메뉴: Subway ▶ Assign Line Sprites
    /// </summary>
    public class LineSpritesAssigner : ScriptableWizard
    {
        [Tooltip("SubwayNetworkData (구간 순서 기준)")]
        public SubwayNetworkData networkData;

        [Tooltip("호선별 멀티플 스프라이트 텍스처 (여러 개 등록 가능)")]
        public Texture2D[] spriteSheets;

        const string K_Network = "LSA_networkData";

        [MenuItem("Subway/Assign Line Sprites")]
        static void Open() =>
            DisplayWizard<LineSpritesAssigner>("호선 스프라이트 자동 할당", "할당");

        void OnEnable()
        {
            var guid = EditorPrefs.GetString(K_Network, "");
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    networkData = AssetDatabase.LoadAssetAtPath<SubwayNetworkData>(path);
            }
        }

        void OnWizardUpdate()
        {
            if (networkData != null)
            {
                var path = AssetDatabase.GetAssetPath(networkData);
                EditorPrefs.SetString(K_Network, AssetDatabase.AssetPathToGUID(path));
            }
        }

        void OnWizardCreate()
        {
            if (networkData == null || spriteSheets == null || spriteSheets.Length == 0)
            {
                EditorUtility.DisplayDialog("오류", "NetworkData와 스프라이트 시트를 지정해주세요.", "확인");
                return;
            }

            // 모든 시트에서 스프라이트 수집: 이름 → Sprite
            var spriteByName = new Dictionary<string, Sprite>();
            foreach (var sheet in spriteSheets)
            {
                if (sheet == null) continue;
                var path = AssetDatabase.GetAssetPath(sheet);
                foreach (var sp in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                    spriteByName[sp.name] = sp;
            }

            // 씬의 UIBezierLine을 (lineId, stationA, stationB) 기준으로 조회용 딕셔너리
            var segMap = new Dictionary<(string lineId, string a, string b), UIBezierLine>();
            foreach (var seg in Object.FindObjectsByType<UIBezierLine>())
                segMap[(seg.lineId, seg.stationA, seg.stationB)] = seg;

            int assigned = 0, missing = 0;

            foreach (var line in networkData.lines)
            {
                if (line == null) continue;
                var stations = line.stations;

                int segCount = line.isCircular ? stations.Count : stations.Count - 1;
                for (int i = 0; i < segCount; i++)
                {
                    string spriteName = $"{line.lineId}_{i}";
                    if (!spriteByName.TryGetValue(spriteName, out var sprite))
                    {
                        Debug.LogWarning($"[LineSpritesAssigner] 스프라이트 없음: {spriteName}");
                        missing++;
                        continue;
                    }

                    string idA = stations[i]?.stationId;
                    string idB = stations[(i + 1) % stations.Count]?.stationId;
                    if (!segMap.TryGetValue((line.lineId, idA, idB), out var seg) &&
                        !segMap.TryGetValue((line.lineId, idB, idA), out seg))
                    {
                        Debug.LogWarning($"[LineSpritesAssigner] 세그먼트 없음: {line.lineId} {idA}-{idB}");
                        missing++;
                        continue;
                    }

                    var img = seg.GetComponent<Image>();
                    if (img == null) { missing++; continue; }

                    Undo.RecordObject(img, "Assign Line Sprite");
                    img.sprite = sprite;
                    img.SetNativeSize();
                    EditorUtility.SetDirty(img);
                    assigned++;
                }
            }

            Debug.Log($"[LineSpritesAssigner] 완료 — 할당 {assigned}개 / 누락 {missing}개");
            EditorUtility.DisplayDialog("완료", $"스프라이트 할당: {assigned}개\n누락: {missing}개", "확인");
        }
    }
}
