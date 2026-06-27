using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 외부IP 씬 효과음 호출을 한 곳에 모은 헬퍼.
    /// 팀원의 SoundManager(Resources/SFX/이름 로드)를 감싼다.
    ///
    /// 오디오 파일은 Resources/SFX/ 아래에 아래 이름으로 넣으면 된다(확장자 제외):
    ///   defect_click, item_pick, item_place, item_rotate_1, item_rotate_2,
    ///   heartbeat(루프), artwork_success, artwork_fail_1, artwork_fail_2,
    ///   chaser_arrived, ui_hover, ui_click, ui_click_disabled
    /// 이름을 바꾸고 싶으면 아래 상수만 고치면 된다.
    /// </summary>
    public static class Sfx
    {
        static void Play(string clip) => SoundManager.Instance?.PlaySFX(clip);

        // 결함
        public static void DefectClick()     => Play("defect_click");

        // 아이템 상호작용
        public static void ItemPick()         => Play("item_pick");
        public static void ItemPlace()        => Play("item_place");
        public static void ItemRotate()       => Play(Random.value < 0.5f ? "item_rotate_1" : "item_rotate_2");

        // 작품활동 결과
        public static void ArtworkSuccess()   => Play("artwork_success");
        public static void ArtworkFail()      => Play(Random.value < 0.5f ? "artwork_fail_1" : "artwork_fail_2");

        // 추격자
        public static void ChaserArrived()    => Play("chaser_arrived");

        // UI
        public static void UiHover()          => Play("ui_hover");
        public static void UiClick()          => Play("ui_click");
        public static void UiClickDisabled()  => Play("ui_click_disabled");

        // 심장박동 — 작품활동 느려지는 구간에 루프. 반환 소스를 HeartbeatStop에 넘겨 정지.
        public static AudioSource HeartbeatStart() => SoundManager.Instance?.PlaySFXLoop("heartbeat");
        public static void HeartbeatStop(AudioSource src)
        {
            if (src != null) SoundManager.Instance?.StopLoopSFX(src);
        }
    }
}