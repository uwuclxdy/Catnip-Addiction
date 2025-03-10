using Photon.Pun;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameManager.Instance.gameStarted) return;
        if (!other.CompareTag("Player")) return;
        if (!PhotonView.Get(other).IsMine) return;

        var player = other.GetComponent<PlayerController>();

        if (player == null || !PhotonNetwork.IsConnected || !PhotonNetwork.LocalPlayer.IsLocal) return;
        var finishTime = Time.timeSinceLevelLoad - GameManager.Instance.startTime;
        var playerID = PhotonNetwork.LocalPlayer.ActorNumber;

        GameManager.Instance.PlayerFinished(playerID, finishTime);
        player.SetSpectatorMode(true);
    }
}
