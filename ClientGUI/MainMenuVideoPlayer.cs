using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Rampastring.Tools;
using System;
using System.IO;

namespace ClientGUI
{
    /// <summary>
    /// Handles video playback for the main menu background with optimized resource usage.
    /// </summary>
    public class MainMenuVideoPlayer : IDisposable
    {
        private VideoPlayer videoPlayer;
        private Video video;
        private Texture2D videoTexture;
        private bool isDisposed;
        private bool isVideoAvailable;
        private readonly string videoPath;

        public MainMenuVideoPlayer(string videoFilePath)
        {
            videoPath = videoFilePath;
        }

        /// <summary>
        /// Gets whether a video is currently loaded and ready to play.
        /// </summary>
        public bool IsVideoAvailable => isVideoAvailable && video != null;

        /// <summary>
        /// Gets whether the video is currently playing.
        /// </summary>
        public bool IsPlaying => videoPlayer != null && videoPlayer.State == MediaState.Playing;

        /// <summary>
        /// Initializes the video player and loads the video file.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to use for texture creation.</param>
        /// <returns>True if the video was loaded successfully, false otherwise.</returns>
        public bool Initialize(GraphicsDevice graphicsDevice)
        {
            if (isDisposed)
                return false;

            try
            {
                // Check if video file exists
                if (!File.Exists(videoPath))
                {
                    Logger.Log($"Main menu video not found at: {videoPath}");
                    isVideoAvailable = false;
                    return false;
                }

                // Initialize video player
                videoPlayer = new VideoPlayer();
                
                // Load the video using URI
                string fullPath = Path.GetFullPath(videoPath);
                Uri videoUri = new Uri(fullPath, UriKind.Absolute);
                video = Video.FromUri(videoUri);

                isVideoAvailable = true;
                Logger.Log($"Main menu video loaded successfully: {videoPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load main menu video: {ex.Message}");
                isVideoAvailable = false;
                return false;
            }
        }

        /// <summary>
        /// Starts or resumes video playback.
        /// </summary>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        public void Play(float volume = 0.0f)
        {
            if (!isVideoAvailable || video == null || videoPlayer == null)
                return;

            try
            {
                if (videoPlayer.State != MediaState.Playing)
                {
                    videoPlayer.IsLooped = true;
                    videoPlayer.IsMuted = volume <= 0.0f;
                    videoPlayer.Volume = volume;
                    videoPlayer.Play(video);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error playing main menu video: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops video playback.
        /// </summary>
        public void Stop()
        {
            if (videoPlayer != null && videoPlayer.State == MediaState.Playing)
            {
                try
                {
                    videoPlayer.Stop();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error stopping main menu video: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Pauses video playback.
        /// </summary>
        public void Pause()
        {
            if (videoPlayer != null && videoPlayer.State == MediaState.Playing)
            {
                try
                {
                    videoPlayer.Pause();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error pausing main menu video: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Updates the video texture with the current frame.
        /// Call this in the Update method before drawing.
        /// </summary>
        /// <returns>The current video frame texture, or null if not available.</returns>
        public Texture2D GetCurrentFrame()
        {
            if (!isVideoAvailable || videoPlayer == null)
                return null;

            try
            {
                if (videoPlayer.State == MediaState.Playing)
                {
                    // Get the current video frame
                    // The VideoPlayer automatically updates its texture
                    videoTexture = videoPlayer.GetTexture();
                }

                return videoTexture;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting video frame: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Releases all resources used by the video player.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
                return;

            try
            {
                Stop();

                if (videoPlayer != null)
                {
                    videoPlayer.Dispose();
                    videoPlayer = null;
                }

                // Video and Texture2D from GetTexture() are managed by XNA and don't need explicit disposal
                video = null;
                videoTexture = null;

                isVideoAvailable = false;
                isDisposed = true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error disposing main menu video player: {ex.Message}");
            }
        }
    }
}
