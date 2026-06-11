using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Move Rejection Messages", fileName = "MoveRejectionMessages")]
    public class MoveRejectionMessages : ScriptableObject
    {
        [TextArea] public string wrongLine      = "환승이 필요합니다. 환승역에서 내리세요.";
        [TextArea] public string wrongDirection = "반대 방향으로 재탑승해야 합니다.";
        [TextArea] public string inactiveLine   = "비활성화된 노선입니다. 환승을 통해 노선을 활성화하세요.";
    }
}
