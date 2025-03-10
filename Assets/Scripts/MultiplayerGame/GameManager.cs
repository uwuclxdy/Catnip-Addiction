using System;
using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using static UnityEngine.Mathf;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;
    private static readonly int isLaying = Animator.StringToHash("IsLaying");

    [Header("Settings")]
    public float countdownDuration = 5f;
    public Transform[] spawnPoints;
    public GameObject countdownUI;
    public TMP_Text countdownText;
    public GameObject finishLine;

    [Header("UI")]
    public TMP_Text gameTimerText;

    [Header("Game State")]
    public bool gameStarted;
    public float startTime;
    private readonly Hashtable _finishTimes = new();
    private bool _localPlayerFinished;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (photonView == null)
            gameObject.AddComponent<PhotonView>();
        finishLine.GetComponent<BoxCollider2D>().enabled = false;
        gameTimerText.enabled = false;
    }

    private void Update()
    {
        if (gameStarted)
            UpdateTimer();
        if (!PhotonNetwork.IsMasterClient || gameStarted) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
            photonView.RPC(nameof(StartCountdown), RpcTarget.All);
    }

    private void UpdateTimer()
    {
        if (!gameStarted || !gameTimerText) return;
        if (_localPlayerFinished) return;

        if (Time.timeSinceLevelLoad <= startTime) return;

        var elapsedTime = Time.timeSinceLevelLoad - startTime;
        DisplayTime(elapsedTime);
    }

    private void DisplayTime(float timeToDisplay)
    {
        float minutes = FloorToInt(timeToDisplay / 60);
        float seconds = FloorToInt(timeToDisplay % 60);

        gameTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    [PunRPC]
    private void StartCountdown()
    {
        gameStarted = true;
        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        countdownUI.SetActive(true);
        var remainingTime = countdownDuration;

        while (remainingTime > 0)
        {
            countdownText.text = "game starts in: " + CeilToInt(remainingTime);
            if (CeilToInt(remainingTime) == 2)
                photonView.RPC(nameof(StandUp), RpcTarget.All);
            remainingTime -= Time.deltaTime;
            yield return null;
        }

        countdownUI.SetActive(false);
        photonView.RPC(nameof(StartGame), RpcTarget.All);
    }

    [PunRPC]
    private void StartGame()
    {
        var players = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var spawnIndex = p.photonView.Owner.ActorNumber % spawnPoints.Length;
            p.Teleport(spawnPoints[spawnIndex].position);
            p.SetMovement(true);
        }
        finishLine.GetComponent<BoxCollider2D>().enabled = true;
        gameTimerText.enabled = true;
        startTime = Time.timeSinceLevelLoad;
    }

    public void PlayerFinished(int playerId, float finishTime)
    {
        if (playerId == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            _localPlayerFinished = true;

            if (gameTimerText != null)
                DisplayTime(finishTime);
        }

        // Debug.Log($"PlayerFinished called for playerId: {playerId}, finishTime: {finishTime}");
        // Debug.Log($"Type of playerId: {playerId.GetType()}, Type of finishTime: {finishTime.GetType()}");
        // Debug.Log($"Current finishTimes Keys Type: {_finishTimes.Keys.GetType()}");

        var alreadyFinished = false;
        foreach (var key in _finishTimes.Keys)
        {
            // Debug.Log($"Key in finishTimes (before cast): {key}, Type of Key: {key.GetType()}"); // Log key before cast
            try
            {
                var keyInt = (int)key;
                if (keyInt != playerId) continue;
                // player that finished was found
                alreadyFinished = true;
                break;
            }
            catch (InvalidCastException e)
            {
                Debug.LogError($"InvalidCastException during key cast: {e.Message}");
                Debug.LogError($"Type of key that caused exception: {key.GetType()}");
                alreadyFinished = true;
                break;
            }
        }
        if (!alreadyFinished)
            photonView.RPC(nameof(UpdateLeaderboard), RpcTarget.All, playerId, finishTime); // Line 85
    }

    [PunRPC]
    private void UpdateLeaderboard(int playerId, float finishTime)
    {
        // Debug.Log($"{PhotonNetwork.CurrentRoom.GetPlayer(playerId).NickName} finished in {finishTime}s");
        // Debug.Log($"Leaderboard keys: {_finishTimes.Keys}\nLeaderboard values: {_finishTimes.Values}");
        var playerName = PhotonNetwork.CurrentRoom.GetPlayer(playerId).NickName;

        var playerDataHash = new Hashtable
        {
            { "playerName", playerName },
            { "finishTime", finishTime }
        };

        _finishTimes.Add(playerId, playerDataHash);

        if (_finishTimes.Count != PhotonNetwork.CurrentRoom.PlayerCount) return;

        var roomProps = new Hashtable { { "LeaderboardData", _finishTimes } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        PhotonNetwork.LoadLevel("Leaderboard");
    }

    [PunRPC]
    private void StandUp()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
            player.animator.SetBool(isLaying, false);
    }
}

[Serializable]
public struct PlayerResultData
{
    public string playerName;
    public float finishTime;

    public PlayerResultData(string name, float time)
    {
        playerName = name;
        finishTime = time;
    }
}
