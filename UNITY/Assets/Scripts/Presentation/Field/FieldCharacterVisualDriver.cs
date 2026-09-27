using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Field;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    /// <summary>Shows the lead party member on foot and drives its locomotion animator from the field controller.</summary>
    public static class FieldCharacterVisualBootstrap
    {
        private const float GroundOffset = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Create();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Create();
        }

        private static void Create()
        {
            var player = Object.FindAnyObjectByType<FieldPlayerController>();
            if (player == null) return;
            var lead = GameSession.Instance.party.FirstOrDefault(member => member.side == Side.Player && member.id != null && member.id.StartsWith("CHR_"));
            if (lead == null) return;
            var prefab = Resources.Load<GameObject>($"Visuals/{lead.id}");
            if (prefab == null)
            {
                Debug.LogWarning($"[Characters] No walk visual at Resources/Visuals/{lead.id}; keeping player greybox.");
                return;
            }

            var existing = player.GetComponentInChildren<FieldCharacterVisualDriver>(true);
            if (existing != null) Object.Destroy(existing.gameObject);
            var visual = Object.Instantiate(prefab, player.transform, false);
            visual.name = $"CharacterVisual_{lead.id}";
            visual.transform.localPosition = new Vector3(0, GroundOffset, 0);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var animator = visual.GetComponentInChildren<Animator>();
            var driver = player.GetComponent<FieldCharacterVisualDriver>() ?? player.gameObject.AddComponent<FieldCharacterVisualDriver>();
            driver.Bind(player, visual, animator);
            Debug.Log($"[Characters] Field visual loaded: {lead.id}");
        }
    }

    public sealed class FieldCharacterVisualDriver : MonoBehaviour
    {
        private FieldPlayerController _player;
        private GameObject _visual;
        private Animator _animator;

        public bool HasVisual => _visual != null;

        public void Bind(FieldPlayerController player, GameObject visual, Animator animator)
        {
            _player = player;
            _visual = visual;
            _animator = animator;
            ApplyMode(player.OnFoot);
        }

        private void OnEnable() => FieldEvents.ModeChanged += ApplyMode;
        private void OnDisable() => FieldEvents.ModeChanged -= ApplyMode;

        private void Update()
        {
            if (_player == null || _animator == null || !_animator.isActiveAndEnabled) return;
            _animator.SetFloat("Speed", _player.Speed);
            _animator.SetBool("Moving", _player.IsMoving);
            _animator.SetBool("Running", _player.IsRunning);
            _animator.SetBool("OnFoot", _player.OnFoot);
            var gaitSpeed = _player.IsRunning ? _player.runSpeed : _player.walkSpeed;
            _animator.speed = _player.OnFoot && _player.IsMoving && gaitSpeed > 0f
                ? Mathf.Clamp(_player.Speed / gaitSpeed, 0.2f, 1.25f)
                : 1f;
            if (_player.OnFoot && _visual != null)
            {
                var greybox = _player.transform.Find("Player_Greybox");
                if (greybox != null && greybox.gameObject.activeSelf) greybox.gameObject.SetActive(false);
            }
        }

        private void ApplyMode(bool onFoot)
        {
            if (_visual != null) _visual.SetActive(onFoot);
            if (_animator == null) return;
            _animator.SetBool("OnFoot", onFoot);
            if (!onFoot) return;
            var greybox = _player != null ? _player.transform.Find("Player_Greybox") : null;
            if (greybox != null) greybox.gameObject.SetActive(false);
        }
    }
}
