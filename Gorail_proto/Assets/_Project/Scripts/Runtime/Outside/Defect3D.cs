using UnityEngine;
using Game.Core;
using Game.Inventory;

namespace Game.UI
{
    /// <summary>
    /// 3D 월드에 놓인 결함(SpriteRenderer). 레이캐스터(Defect3DRaycaster)가 호버/클릭을 알려준다.
    /// 호버 시 hoverSprite로 교체, 클릭 시 작품활동 패널(ArtworkScreen)을 연다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Defect3D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("평상시 스프라이트(DDP Defect). 비우면 시작 시 현재 스프라이트를 사용.")]
        [SerializeField] private Sprite normalSprite;
        [Tooltip("호버 시 스프라이트(DDP Defect Hover).")]
        [SerializeField] private Sprite hoverSprite;

        void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (normalSprite == null && spriteRenderer != null) normalSprite = spriteRenderer.sprite;
        }

        public void SetHover(bool on)
        {
            if (spriteRenderer == null) return;
            if (on && hoverSprite == null)
                Debug.LogWarning($"[Defect3D] '{name}' Hover Sprite 미연결 — 호버해도 안 바뀜.");
            spriteRenderer.sprite = (on && hoverSprite != null) ? hoverSprite : normalSprite;
        }

        public void Click()
        {
            Sfx.DefectClick();
            var screen = FindFirstObjectByType<ArtworkScreen>();
            if (screen != null) screen.Open();
            else Debug.LogWarning("[Defect3D] ArtworkScreen을 찾지 못함 — 작품활동 패널 열기 실패.");
        }
    }
}