using System;
using System.Collections.Generic;
using TootTally.Spectating;
using TootTally.Utils.Helpers;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.UI;
using static TootTally.Spectating.SpectatingManager;

namespace TootTally.TournamentHost
{
    public class TournamentGameplayController : MonoBehaviour
    {
        private GameController _gcInstance;
        private GameObject _canvasObject, _container;
        private Canvas _canvas;
        private Camera _camera;
        private Rect _bounds;
        private SpectatingSystem _spectatingSystem;
        private GameObject _pointer;
        private CanvasGroup _pointerGlowCanvasGroup;

        private bool _isTooting;

        public void Initialize(GameController gcInstance, Camera camera, Rect bounds, SpectatingSystem spectatingSystem)
        {
            _gcInstance = gcInstance;
            _camera = camera;
            _bounds = bounds;
            camera.pixelRect = bounds;
            camera.transform.localPosition = new Vector2(-4.45f, 0);
            _spectatingSystem = spectatingSystem;
            _spectatingSystem.OnWebSocketOpenCallback = OnSpectatingConnect;

            _canvasObject = new GameObject($"TournamentGameplayCanvas{spectatingSystem.GetSpectatorUserId}");
            _container = GameObject.Instantiate(_canvasObject, _canvasObject.transform);

            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

            var rect = _container.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(_bounds.position.x - _bounds.width, _bounds.position.y);
            rect.anchoredPosition =  _bounds.position;
            rect.sizeDelta = new Vector2(_bounds.width, _bounds.height);
            rect.pivot = Vector2.one;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _pointer = GameObject.Instantiate(gcInstance.pointer, _container.transform);
            _pointerGlowCanvasGroup = _pointer.transform.Find("note-dot-glow").GetComponent<CanvasGroup>();

            _frameIndex = 0;
            _tootIndex = 0;
            _lastFrame = null;
            _currentFrame = new SocketFrameData() { time = 0, noteHolder = 0, pointerPosition = 0 };
            _currentTootData = new SocketTootData() { time = 0, isTooting = false, noteHolder = 0 };
            _isTooting = false;
        }

        private void OnSpectatingConnect(SpectatingSystem sender)
        {
            _spectatingSystem.OnSocketUserStateReceived = OnUserStateReceived;
            _spectatingSystem.OnSocketFrameDataReceived = OnFrameDataReceived;
            _spectatingSystem.OnSocketTootDataReceived = OnTootDataReceived;
            _spectatingSystem.OnSocketNoteDataReceived = OnNoteDataReceived;
        }

        private void Update()
        {
            _spectatingSystem?.UpdateStacks();
            HandlePitchShift();
            PlaybackSpectatingData(_gcInstance);
        }

        public void OnGetScoreAverage()
        {

        }

        private List<SocketFrameData> _frameData = new List<SocketFrameData>();
        private List<SocketTootData> _tootData = new List<SocketTootData>();
        private List<SocketNoteData> _noteData = new List<SocketNoteData>();
        private int _frameIndex;
        private int _tootIndex;
        private SocketFrameData _lastFrame, _currentFrame;
        private SocketTootData _currentTootData;

        private void OnUserStateReceived(SocketUserState stateData)
        {

        }

        private void OnFrameDataReceived(SocketFrameData frameData)
        {
            _frameData.Add(frameData);
        }

        private void OnTootDataReceived(SocketTootData tootData)
        {
            _tootData.Add(tootData);
        }

        private void OnNoteDataReceived(SocketNoteData noteData)
        {
            _noteData.Add(noteData);
        }

        public void PlaybackSpectatingData(GameController __instance)
        {
            if (_frameData == null || _tootData == null) return;

            var currentMapPosition = __instance.musictrack.time;

            if (_frameData.Count > 0)
                PlaybackFrameData(currentMapPosition);

            if (_tootData.Count > 0)
                PlaybackTootData(currentMapPosition);

            if (_frameData.Count > _frameIndex && _lastFrame != null && _currentFrame != null)
                InterpolateCursorPosition(currentMapPosition);


        }

        private void InterpolateCursorPosition(float currentMapPosition)
        {
            var newCursorPosition = EasingHelper.Lerp(_lastFrame.pointerPosition, _currentFrame.pointerPosition, (float)((_lastFrame.time - currentMapPosition) / (_lastFrame.time - _currentFrame.time)));
            SetCursorPosition(newCursorPosition);
        }

        private void PlaybackFrameData(float currentMapPosition)
        {
            if (_lastFrame != _currentFrame && currentMapPosition >= _currentFrame.time)
                _lastFrame = _currentFrame;

            if (_frameData.Count > _frameIndex && (_currentFrame == null || currentMapPosition >= _currentFrame.time))
            {
                _frameIndex = _frameData.FindIndex(_frameIndex > 1 ? _frameIndex - 1 : 0, x => currentMapPosition < x.time);
                if (_frameData.Count > _frameIndex && _frameIndex != -1)
                    _currentFrame = _frameData[_frameIndex];
            }
        }

        public void PlaybackTootData(float currentMapPosition)
        {
            if (currentMapPosition >= _currentTootData.time && _isTooting != _currentTootData.isTooting)
            {
                _isTooting = _currentTootData.isTooting;
                if (_isTooting)
                {
                    _currentNoteSound.time = 0f;
                    _currentNoteSound.volume = _currentVolume = 1f;
                    PlayNote();
                    LeanTween.alphaCanvas(_pointerGlowCanvasGroup, 0.95f, 0.05f);
                }
                else
                {
                    LeanTween.alphaCanvas(_pointerGlowCanvasGroup, 0f, 0.05f);
                }
            }

            if (_tootData.Count > _tootIndex && currentMapPosition >= _currentTootData.time)
                _currentTootData = _tootData[_tootIndex++];


        }

        private void SetCursorPosition(float newPosition)
        {
            _pointer.GetComponent<RectTransform>().anchoredPosition = new Vector2(28, newPosition);
        }

        private float _currentVolume;
        private float _noteStartPosition;
        private AudioSource _currentNoteSound;
        private AudioClip[] _tClips;

        public void CopyAllAudioClips()
        {
            _currentNoteSound = GameObject.Instantiate(_gcInstance.currentnotesound);
            _tClips = _currentNoteSound.gameObject.transform.GetChild(0).gameObject.GetComponent<AudioClipsTromb>().tclips;
        }

        private void PlayNote()
        {
            float num = 9999f;
            int num2 = 0;
            for (int i = 0; i < 15; i++)
            {
                float num3 = Mathf.Abs(_gcInstance.notelinepos[i] - _pointer.GetComponent<RectTransform>().anchoredPosition.y);
                if (num3 < num)
                {
                    num = num3;
                    num2 = i;
                }
            }
            _noteStartPosition = _gcInstance.notelinepos[num2];
            _currentNoteSound.clip = _tClips[Mathf.Abs(num2 - 14)];
            _currentNoteSound.Play();
        }

        private void StopNote()
        {
            _currentNoteSound.Stop();
        }

        private void HandlePitchShift()
        {
            if (_tClips == null && _currentNoteSound == null) return;

            var pointerPos = _pointer.GetComponent<RectTransform>().anchoredPosition.y;

            if (!_isTooting)
            {
                if (_currentVolume < 0f)
                    _currentVolume = 0f;
                else if (_currentVolume > 0f)
                    _currentVolume -= Time.deltaTime * 18f;

                _currentNoteSound.volume = _currentVolume;
            }
            if (_isTooting)
            {
                if (_currentNoteSound.time > _currentNoteSound.clip.length - 1.25f)
                    _currentNoteSound.time = 1f;

                float num11 = Mathf.Pow(_noteStartPosition - pointerPos, 2f) * 6.8E-06f;
                float num12 = (_noteStartPosition - pointerPos) * (1f + num11);
                if (num12 > 0f)
                {
                    num12 = (_noteStartPosition - pointerPos) * 1.392f;
                    num12 *= 0.5f;
                }
                float num13 = 1f - num12 * 0.00501f;
                if (num13 > 2f)
                    num13 = 2f;
                else if (num13 < 0.5f)
                    num13 = 0.5f;

                _currentNoteSound.pitch = num13;
            }
        }
    }
}
