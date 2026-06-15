using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 범용 모달 다이얼로그 싱글톤.
    /// 씬에 ModalDialog 컴포넌트를 가진 GameObject를 하나 두고 인스펙터를 연결한다.
    ///
    /// 사용 예:
    /// ModalDialog.Show("게임을 종료하시겠습니까?",
    ///     ("예",    () => Application.Quit()),
    ///     ("아니오", null));
    /// </summary>
    public class ModalDialog : MonoBehaviour
    {
        public static ModalDialog Instance { get; private set; }

        [Header("UI 연결")]
        [SerializeField] private GameObject        root;        // 모달 전체 (평소 비활성)
        [SerializeField] private TextMeshProUGUI   messageTxt;
        [SerializeField] private Button[]          buttons;     // 버튼 배열 (순서대로 사용)
        [SerializeField] private TextMeshProUGUI[] buttonTxts;  // 버튼별 라벨 TMP

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (root != null) root.SetActive(false);
        }

        /// <summary>
        /// 모달을 표시한다.
        /// </summary>
        /// <param name="message">본문 메세지</param>
        /// <param name="buttonDefs">
        /// (라벨, 콜백) 쌍. 콜백이 null이면 닫기만 한다.
        /// 인스펙터에 연결된 버튼 수를 초과하는 항목은 무시된다.
        /// </param>
        public static void Show(string message, params (string label, Action callback)[] buttonDefs)
        {
            if (Instance == null) { Debug.LogWarning("[ModalDialog] 씬에 인스턴스가 없습니다."); return; }
            Instance.Display(message, buttonDefs);
        }

        public static void Hide()
        {
            if (Instance != null) Instance.Close();
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        void Display(string message, (string label, Action callback)[] buttonDefs)
        {
            if (messageTxt != null) messageTxt.text = message;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                bool active = i < buttonDefs.Length;
                buttons[i].gameObject.SetActive(active);

                if (!active) continue;

                if (buttonTxts != null && i < buttonTxts.Length && buttonTxts[i] != null)
                    buttonTxts[i].text = buttonDefs[i].label;

                buttons[i].onClick.RemoveAllListeners();
                var cb = buttonDefs[i].callback;
                buttons[i].onClick.AddListener(() =>
                {
                    Close();
                    cb?.Invoke();
                });
            }

            if (root != null) root.SetActive(true);
        }

        void Close()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
