using UnityEngine;
using Game.Core;
using Game.Inventory;
using Game.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// OutsideScene 진입 시 현재 역의 IpCanvasData.model3D(3D 모델 프리팹)를 마운트 지점에 Instantiate한다.
    /// - 프리팹 안 결함의 Billboard는 씬 카메라 참조가 안 담기므로 런타임에 billboardCamera를 주입한다.
    /// - 쿨타임(이미 작품 완료한 역)이면 결함을 숨기고, 작품 성공 시에도 숨긴다.
    /// - model3D가 없는 IP는 2D 배경 폴백(background2D)을 켠다.
    /// 카메라 리그·조명·바닥·Defect3DRaycaster·OutsideViewController는 씬에 두는 공유물이라 여기서 안 만든다.
    /// </summary>
    public class IpModelLoader : MonoBehaviour
    {
        [Tooltip("3D 모델을 Instantiate할 부모. 비우면 이 오브젝트.")]
        [SerializeField] private Transform mountPoint;

        [Tooltip("프리팹 결함의 Billboard에 주입할 카메라(보통 ViewCamera). 비우면 Camera.main.")]
        [SerializeField] private Camera billboardCamera;

        [Tooltip("역에 ipCanvas가 없을 때 쓸 기본 IP.")]
        [SerializeField] private IpCanvasData defaultIp;

        [Tooltip("model3D가 없는 IP에서 켤 2D 배경 루트(GroundBaseView 등). 3D일 땐 끔.")]
        [SerializeField] private GameObject background2D;

        [Tooltip("3D 진입 시 끌 SubwayScene(또는 다른 씬)의 오브젝트 이름. 비우면 안 끔.")]
        [SerializeField] private string hideObjectName = "Background";

        GameObject _hiddenSubwayBg;

        GameObject _instance;
        ArtworkSystem Artwork => GameCore.Instance?.Artwork;

        void Start()
        {
            var ip = ResolveIp();

            if (ip != null && ip.model3D != null)
            {
                if (background2D != null) background2D.SetActive(false);
                HideSubwayBackground();
                SpawnModel(ip);
                if (Artwork != null) Artwork.ArtworkFinished += OnArtworkFinished;
            }
            else
            {
                // 2D 폴백 — 기존 GroundBaseView 경로 사용
                if (background2D != null) background2D.SetActive(true);
            }
        }

        void OnDestroy()
        {
            if (Artwork != null) Artwork.ArtworkFinished -= OnArtworkFinished;
            if (_instance != null) Destroy(_instance);
            RestoreSubwayBackground();   // 외부에서 나갈 때 SubwayScene 배경 복구
        }

        // SubwayScene 등 다른 씬의 배경 오브젝트를 이름으로 찾아 끔(additive라 인스펙터 직접 연결 불가)
        void HideSubwayBackground()
        {
            if (string.IsNullOrEmpty(hideObjectName)) return;
            var go = GameObject.Find(hideObjectName);
            if (go != null && go.activeSelf)
            {
                _hiddenSubwayBg = go;
                go.SetActive(false);
            }
        }

        void RestoreSubwayBackground()
        {
            if (_hiddenSubwayBg != null) { _hiddenSubwayBg.SetActive(true); _hiddenSubwayBg = null; }
        }

        void SpawnModel(IpCanvasData ip)
        {
            var parent = mountPoint != null ? mountPoint : transform;
            _instance = Instantiate(ip.model3D, parent);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;

            // 빌보드 카메라 주입(프리팹엔 씬 카메라 참조가 안 담김)
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam != null)
                foreach (var bb in _instance.GetComponentsInChildren<Billboard>(true))
                    bb.SetTarget(cam);

            // 쿨타임: 이미 작품 완료한 역이면 결함 숨김(§6)
            if (IsDone()) HideDefects();
        }

        void OnArtworkFinished(bool succeeded, float fameGain, bool interrupted)
        {
            // 성공(중단 아님) 시 결함 숨김 — 2D DefectController와 동일한 동작
            if (succeeded && !interrupted) HideDefects();
        }

        void HideDefects()
        {
            if (_instance == null) return;
            foreach (var d in _instance.GetComponentsInChildren<Defect3D>(true))
                d.gameObject.SetActive(false);
        }

        bool IsDone()
        {
            var plat = GameCore.Instance?.Platform;
            var stn = GameCore.Instance?.Space?.CurrentStationId;
            return plat != null && !string.IsNullOrEmpty(stn) && plat.IsArtworkDone(stn);
        }

        IpCanvasData ResolveIp()
        {
            var st = GameCore.Instance?.Graph?.Graph?.GetStation(GameCore.Instance?.Space?.CurrentStationId);
            return (st != null && st.ipCanvas != null) ? st.ipCanvas : defaultIp;
        }
    }
}