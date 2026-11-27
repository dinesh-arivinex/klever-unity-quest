// AVProVideoErrorHandler.cs
using UnityEngine;
using RenderHeads.Media.AVProVideo;
using UnityEngine.Video;

public class AVProVideoErrorHandler : MonoBehaviour
{
    [Tooltip("Drag the MediaPlayer component here to link the events.")]
    public MediaPlayer mediaPlayer;

    void Start()
    {
        // 1. Check if the MediaPlayer is assigned
        if (mediaPlayer == null)
        {
            Debug.LogError("AVProVideoErrorHandler: MediaPlayer component not assigned.", this);
            return;
        }

        // 2. Subscribe to the event *without* modifying the MediaPlayer script
        mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
        Debug.Log($"Subscribed to events for MediaPlayer: {mediaPlayer.name}");
    }

    // This function will be called for all events (Open, Play, Error, etc.)
    private void OnMediaPlayerEvent(MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
    {
        if (eventType == MediaPlayerEvent.EventType.Error)
        {
            // 3. The error event has occurred, now call your custom function
            HandleVideoError(Helper.GetErrorMessage(errorCode), mp);
        }
    }

    public void HandleVideoError(string errorMessage, MediaPlayer player)
    {
        Debug.LogError($"[Video] {player.name}: {errorMessage}");
        var manager = VideoManager.Instance;
        if (manager != null && manager.mediaPlayer == player) manager.FailPlayback();
        else player.CloseMedia();
    }

    void OnDestroy()
    {
        // 4. Always unsubscribe when the object is destroyed to prevent memory leaks
        if (mediaPlayer != null)
        {
            mediaPlayer.Events.RemoveListener(OnMediaPlayerEvent);
        }
    }
}