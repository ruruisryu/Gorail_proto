using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 혼잡도 HUD — 우상단 상시 표시 (UI기획서 §2-2).
    /// 인스펙터에서 각 단계 박스(Image)와 라벨(TMP)을 직접 연결한다.
    /// 외부(지상) 씬 진입 시 비활성화.
    /// </summary>
    public class CongestionHud : MonoBehaviour
    {
        [System.Serializable]
        public struct LevelBox
        {
            public RectTransform   root;
            public Image           bg;
            public Color           activeColor;
            public Color           inactiveColor;
            public TextMeshProUGUI numTxt;
            public TextMeshProUGUI labelTxt;
        }

        [Header("단계 박스 (인덱스 0=여유 ~ 3=혼잡)")]
        [SerializeField] private LevelBox[] levels = new LevelBox[4];

        [SerializeField] private Color numActiveColor;
        [SerializeField] private Color numInactiveColor;

        private int _currentLevel = 1;
        public int CongestionLevel
        {
            get => _currentLevel;
            set { _currentLevel = Mathf.Clamp(value, 0, 3); Refresh(); }
        }

        void Start()
        {
            Refresh();
            var core = GameCore.Instance;
            if (core?.Space != null)
                core.Space.SpaceChanged += OnSpaceChanged;
        }

        void OnDestroy()
        {
            var core = GameCore.Instance;
            if (core?.Space != null)
                core.Space.SpaceChanged -= OnSpaceChanged;
        }

        void OnSpaceChanged(GameSpace space)
        {
            bool visible = space != GameSpace.Ground;
            gameObject.SetActive(visible);
        }

        void Refresh()
        {
            for (int i = 0; i < levels.Length; i++)
            {
                bool isCurrent = i == _currentLevel;
                ref var box = ref levels[i];

                // 현재 단계: 높이 15px 돌출
                if (box.root != null)
                {
                    var size = box.root.sizeDelta;
                    box.root.sizeDelta        = new Vector2(size.x, isCurrent ? 75f : 60f);
                    var pos = box.root.anchoredPosition;
                    box.root.anchoredPosition = new Vector2(pos.x, isCurrent ? -7.5f : 0f);
                }

                if (box.bg != null)
                    box.bg.color = isCurrent ? box.activeColor : box.inactiveColor;

                if (box.numTxt != null)
                    box.numTxt.color = isCurrent ? numActiveColor : numInactiveColor;

                if (box.labelTxt != null)
                    box.labelTxt.gameObject.SetActive(isCurrent);
            }
        }
    }
}
